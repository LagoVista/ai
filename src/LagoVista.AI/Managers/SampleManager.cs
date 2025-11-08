// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 3477de44635fae1159f517ce45b1c5e99f91c9c8fb2b10813d4b91e51dae7985
// IndexVersion: 2
// --- END CODE INDEX META ---
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LagoVista.AI.Models.TrainingData;
using LagoVista.Core;
using LagoVista.Core.Exceptions;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.PlatformSupport;
using LagoVista.Core.Validation;

namespace LagoVista.AI.Managers
{
    public class SampleManager : ManagerBase, ISampleManager
    {
        ISampleMediaRepo _sampleMediaRepo;
        ISampleRepo _sampleRepo;
        ILabelRepo _labelRepo;
        ISampleLabelRepo _sampleLabelRepo;
        ILabelSampleRepo _labelSampleRepo;

        public SampleManager(ISampleRepo sampleRepo, ISampleMediaRepo sampleMediaRepo, ISampleLabelRepo sampleLabelRepo, ILabelSampleRepo labelSampleRepo,
            ILabelRepo repo, ILogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security)
            : base(logger, appConfig, dependencyManager, security)
        {
            this._sampleMediaRepo = sampleMediaRepo ?? throw new NullReferenceException(nameof(sampleMediaRepo));
            this._sampleRepo = sampleRepo ?? throw new NullReferenceException(nameof(sampleRepo));
            this._labelRepo = repo ?? throw new NullReferenceException(nameof(repo));
            this._sampleLabelRepo = sampleLabelRepo ?? throw new NullReferenceException(nameof(sampleLabelRepo));
            this._labelSampleRepo = labelSampleRepo ?? throw new NullReferenceException(nameof(labelSampleRepo));
        }

        public async Task<InvokeResult> AddLabelForSampleAsync(string sampleId, string labelId, EntityHeader org, EntityHeader user)
        {
            var sample = await this._sampleRepo.GetSampleAsync(sampleId, org.Id);
            return await AddLabelForSampleAsync(sample, labelId, org, user);
        }

        public async Task<InvokeResult> AddLabelForSampleAsync(Sample sample, string labelId, EntityHeader org, EntityHeader user)
        {
            var labelDetails = await _labelRepo.GetLabelAsync(labelId);
            var labels = await _labelSampleRepo.GetLabelsForSampleAsync(sample.RowKey);
            if(labels.Where(lbl=>lbl.LabelId == labelId).Any())
            {
                return InvokeResult.FromError("Label already attached.");
            }

            var label = new SampleLabel()
            {
                RowKey = $"{sample.RowKey}-{labelId}",
                PartitionKey = $"{labelId}-{sample.ContentType.Replace("/","-")}",
                SampleId = sample.RowKey,
                FileName = sample.FileName,
                ContentSize = sample.ContentSize,
                ContentType = sample.ContentType,
                Name = sample.Name,
                LabelId = labelId,
                Label = labelDetails.Key,
                CreatedById = user.Id,
                CreationDate = DateTime.UtcNow.ToJSONString(),
                OwnerOrganizationId = org.Id,
                OwnerOrganizationName = org.Text,
            };

            await _sampleLabelRepo.AddSampleLabelAsync(label);

            var labelSample = new LabelSample()
            {
                RowKey = $"{labelId}-{sample.RowKey}",
                PartitionKey = sample.RowKey,
                Label = labelDetails.Key,
                LabelId = labelId,
            };

            await _labelSampleRepo.AddLabelSampleAsync(labelSample);

            return InvokeResult.Success;
        }

        public async Task<InvokeResult<Sample>> AddSampleAsync(byte[] sampleBytes, string fileName, string contentType, List<string> labelIds, EntityHeader org, EntityHeader user)
        {
            var now = DateTime.UtcNow;

            var timeStamp = now.ToJSONString();

            var file = new FileInfo(fileName);

            var sample = new Sample()
            {
                RowKey = Guid.NewGuid().ToId(),
                PartitionKey = org.Id,
                Name = file.Name,
                Key = $"sample{now.Ticks}",
                FileName = fileName,
                OwnerOrganizationId = org.Id,
                OwnerOrganizationName = org.Text,
                ContentType = contentType,
                ContentSize = sampleBytes.Length,
                CreatedById = user.Id,
                CreationDate = timeStamp,
                LastUpdatedDate = timeStamp,
                CreatedByName = user.Text,
                LastUpdatedById = user.Id,
                LastUpdatedByName = user.Text,
            };

            await _sampleRepo.AddSampleAsync(sample);
            await _sampleMediaRepo.AddSampleAsync(org.Id, sample.RowKey, sampleBytes);

            await AuthorizeAsync(user, org, "AddSampleAsync", sample);

            foreach (var labelId in labelIds)
            {
                await AddLabelForSampleAsync(sample.RowKey, labelId, org, user);
            }

            return InvokeResult<Sample>.Create(sample);
        }

        public async Task<InvokeResult<byte[]>> GetSampleAsync(string sampleId, EntityHeader org, EntityHeader user)
        {
            // leave this to do a validtion check.
            await GetSampleDetailAsync(sampleId, org, user);
            return await _sampleMediaRepo.GetSampleAsync(org.Id, sampleId);
        }

        public async Task<SampleDetail> GetSampleDetailAsync(string sampleId, EntityHeader org, EntityHeader user)
        {
            var sample = await _sampleRepo.GetSampleAsync(sampleId, org.Id);
            var labels = await _labelSampleRepo.GetLabelsForSampleAsync(sampleId);

            var detail = SampleDetail.FromSample(sample);
            detail.Labels = new List<EntityHeader>(labels.Select(lbl => EntityHeader.Create(lbl.LabelId, lbl.Label)));
            await AuthorizeAsync(detail, AuthorizeResult.AuthorizeActions.Read, user, org);

            return detail;
        }

        public async Task<ListResponse<SampleSummary>> GetSamplesForLabelAsync(string labelId, string contentType, EntityHeader org, EntityHeader user, ListRequest request)
        {
            var label = await _labelRepo.GetLabelAsync(labelId);
            await AuthorizeAsync(label, AuthorizeResult.AuthorizeActions.Read, user, org);

            var samples = await _sampleLabelRepo.GetSamplesForLabelAsync(labelId, contentType, request);

            return new ListResponse<SampleSummary>()
            {
                Model = samples.Model.Select(smp => SampleSummary.FromSampleLabel(smp)),
                NextPartitionKey = samples.NextPartitionKey,
                NextRowKey = samples.NextRowKey,
                PageCount = samples.PageCount,
                PageIndex = samples.PageIndex,
                PageSize = samples.PageSize
            };
        }

        public async Task<InvokeResult> RemoveLabelFromSampleAsync(string sampleId, string labelId, EntityHeader org, EntityHeader user)
        {
            // this will trigger 
            var sample = await GetSampleDetailAsync(sampleId, org, user);
            await AuthorizeAsync(sample, AuthorizeResult.AuthorizeActions.Update, user, org);

            await _labelSampleRepo.RemoveLabelSampleAsync(labelId, sampleId);
            await _sampleLabelRepo.RemoveSampleLabelAsync(labelId, sampleId);

            return InvokeResult.Success;
        }

        public async Task<InvokeResult> UpdateSampleAsync(string sampleId, byte[] sampleBytes, EntityHeader org, EntityHeader user)
        {
            var sample = await GetSampleDetailAsync(sampleId, org, user);
            await AuthorizeAsync(sample, AuthorizeResult.AuthorizeActions.Update, user, org);

            await _sampleMediaRepo.UpdateSampleAsync(org.Id, sample.FileName, sampleBytes);

            return InvokeResult.Success;
        }
    }
}

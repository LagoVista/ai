// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 1d36ea1a7889fc19aae4e5aab18a0a006bf036ae544f402632f346cb2eb8ed61
// IndexVersion: 2
// --- END CODE INDEX META ---
using System.Threading.Tasks;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Managers;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.Logging.Loggers;
using LagoVista.ProjectManagement.Models;
using LagoVista.Core.Models.UIMetaData;
using LagoVista.Core.Attributes;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ReverseMarkdown.Converters;
using System;
using LagoVista.UserAdmin.Models.Orgs;
using static Antlr4.Runtime.Atn.SemanticContext;
using LagoVista.MediaServices.Interfaces;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using LagoVista.UserAdmin.Interfaces.Repos.Orgs;
using LagoVista.IoT.Deployment.Admin.Services;
using LagoVista.UserAdmin.Models.Users;
using System.Text;
using Microsoft.VisualStudio.Services.Aad;


namespace LagoVista
{
    public static class StringUtils
    {
        public static string SplitTagName(this string tag)
        {
            string output = "";

            foreach (char letter in tag)
            {
                if (Char.IsUpper(letter) && output.Length > 0)
                    output += " " + letter;
                else
                    output += letter;
            }

            return output;
        }

        public static string ToBracketedTag(this string tag)
        {
            return $"[{tag}]";
        }
    }
}

namespace LagoVista.ProjectManagement.Managers
{
    public class DocumentTemplateManager : ManagerBase, IDocumentTemplateManager
    {
        readonly IDocumentTemplateRepo _templateRepo;
        readonly IMediaServicesManager _mediaServices;
        readonly IOrgLocationRepo _orgLocationRepo;

        public DocumentTemplateManager(IDocumentTemplateRepo templateRepo, IMediaServicesManager mediaServices, IOrgLocationRepo orgLocationRepo, IAdminLogger logger, IAppConfig appConfig, IDependencyManager dependencyManager, ISecurity security)
            : base(logger, appConfig, dependencyManager, security)
        {
            _templateRepo = templateRepo ?? throw new ArgumentNullException(nameof(templateRepo));
            _mediaServices = mediaServices ?? throw new ArgumentNullException(nameof(mediaServices));
            _orgLocationRepo = orgLocationRepo ?? throw new ArgumentNullException(nameof(orgLocationRepo));
        }

        public async Task<InvokeResult> AddDocumentTemplateAsync(DocumentTemplate documentTemplate, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(documentTemplate, Actions.Create);
            await AuthorizeAsync(documentTemplate, AuthorizeResult.AuthorizeActions.Create, user, org);
            await _templateRepo.AddDocumentTemplateAsync(documentTemplate);
            return InvokeResult.Success;
        }

        public async Task<InvokeResult> DeleteDocumentTemplateAsync(string id, EntityHeader org, EntityHeader user)
        {
            var Competitoc = await _templateRepo.GetDocumentTemplateAsync(id);
            await AuthorizeAsync(Competitoc, AuthorizeResult.AuthorizeActions.Delete, user, org);

            await _templateRepo.DeleteDocumentTemplateAsync(id);
            return InvokeResult.Success;
        }

        public async Task<DocumentTemplate> GetDocumentTemplateAsync(string id, EntityHeader org, EntityHeader user)
        {
            var DocumentTemplate = await _templateRepo.GetDocumentTemplateAsync(id);
            return DocumentTemplate;
        }

        public const string OrganizationLogo = "OrganizationLogo";
        public const string OrganizationName = "OrganizationName";
        public const string OrganizationState = "OrganizationState";
        public const string OrganizationPhone = "OrganiationPhone";
        public const string OrganizationWebSite = "OrganizationWebSite";
        public const string OrganizationPrimaryLocation = "OrganizationPrimaryLocation";
        public const string OrganizationBillingLocation = "OrganizationBillingLocation";
        public const string AccountManagerName = "AccountManagerName";
        public const string BusinessDevelopmentRepName = "BusinessDevelopmentRepName";
        public const string PageBreak = "PageBreak";
        public const string CurrentDate = "CurrentDate";
        public const string CurrentYear = "CurrentYear";

        public const string CustomerName = "CustomerName";
        public const string CustomerContactTitle = "CustomerContactTitle";
        public const string CustomerContactName = "CustomerContactName";
        public const string CustomerState = "CustomerState";
        public const string CustomerAddress = "CustomerAddress";

        public static List<ReplaceableTag> CustomerTags
        {
            get => new List<ReplaceableTag>()
            {
                new ReplaceableTag() { Tag = CustomerName, Title = CustomerName.SplitTagName()},
                new ReplaceableTag() { Tag = CustomerContactName, Title = CustomerContactName.SplitTagName()},
                new ReplaceableTag() { Tag = CustomerContactTitle, Title = CustomerContactTitle.SplitTagName()},
                new ReplaceableTag() { Tag = CustomerState, Title = CustomerState.SplitTagName()},
                new ReplaceableTag() { Tag = CustomerAddress, Title = CustomerAddress.SplitTagName()},
            };
        }
        
        public static List<ReplaceableTag> CommonTags
        {
            get => new List<ReplaceableTag>()
            {
                new ReplaceableTag() { Tag = CurrentDate, Title = CurrentDate.SplitTagName()},
                new ReplaceableTag() { Tag = CurrentYear, Title = CurrentYear.SplitTagName()},
                new ReplaceableTag() { Tag = PageBreak, Title = PageBreak.SplitTagName()},
                new ReplaceableTag() { Tag = OrganizationLogo, Title = OrganizationLogo.SplitTagName()},
                new ReplaceableTag() { Tag = OrganizationName, Title = OrganizationName.SplitTagName()},
                new ReplaceableTag() { Tag = OrganizationWebSite, Title = OrganizationWebSite.SplitTagName()},
                new ReplaceableTag() { Tag = OrganizationPhone, Title = OrganizationPhone.SplitTagName()},
                new ReplaceableTag() { Tag = OrganizationState, Title = OrganizationState.SplitTagName()},
                new ReplaceableTag() { Tag = OrganizationPrimaryLocation, Title = OrganizationPrimaryLocation.SplitTagName()},
                new ReplaceableTag() { Tag = OrganizationBillingLocation, Title = OrganizationBillingLocation.SplitTagName()},
                new ReplaceableTag() { Tag = AccountManagerName, Title = AccountManagerName.SplitTagName()},
                new ReplaceableTag() { Tag = BusinessDevelopmentRepName, Title = BusinessDevelopmentRepName.SplitTagName()},
            };
        }

        public const string SignatureBlock = "SignatureBlock";
        public const string InitialsBlock = "InitialsBlock";

        private string GetLabelValueHtml(string label, string value)
        {
            var signatureBlock = new StringBuilder();
            signatureBlock.Append("<div style='display:flex'>");
            signatureBlock.Append($"\t<div style='font-weight:bold;width:90px'>{label}:</div>");
            signatureBlock.Append($"\t<div >{value}</div>");
            signatureBlock.Append("</div>");
            return signatureBlock.ToString();
        }

        public string GetAppUserSignatureBlock(AppUser appUser)
        {
            var signatureBlock = new StringBuilder();
            signatureBlock.AppendLine($"<div data-org-id='${appUser.CurrentOrganization.Id}' data-app-user-id='{appUser.Id}' style='margin-bottom:20px' >");
            signatureBlock.AppendLine($"\t<h5>{appUser.CurrentOrganization.Text}</h5>");
            signatureBlock.AppendLine(@"	<div style=""border:1pt solid #202020;border-radius:5px;width:300px;height:100px;margin-bottom:10px"">");
            signatureBlock.AppendLine($"\t\t<div style=\"background:white;margin-top:85px;margin-left:10px;width:fit-content;text-align:center\">{appUser.Name}</div>");
            signatureBlock.AppendLine("\t</div>");
            
            if(!String.IsNullOrEmpty(appUser.Title))
                signatureBlock.AppendLine(GetLabelValueHtml(Resources.PMResources.Common_Title, appUser.Title));

            if (!string.IsNullOrEmpty(appUser.Email))
                signatureBlock.AppendLine(GetLabelValueHtml(Resources.PMResources.Common_Email, appUser.Email));

            if (!string.IsNullOrEmpty(appUser.PhoneNumber))
                signatureBlock.AppendLine(GetLabelValueHtml(Resources.PMResources.Common_PhoneNumber, appUser.PhoneNumber));

            signatureBlock.AppendLine("</div>");

            return signatureBlock.ToString();
        }

        public string GetCustomerContactSignatureBlock(string companyId, string contactId, string companyName, string contactName, string email, string phoneNumber, string title)
        {
            var signatureBlock = new StringBuilder();
            signatureBlock.AppendLine($"<div data-company-id='${companyId}' data-contact-id='{contactId}' style='margin-bottom:20px' >");
            signatureBlock.AppendLine($"\t<h5>{companyName}</h5>");
            signatureBlock.AppendLine(@"	<div style=""border:1pt solid #202020;border-radius:5px;width:300px;height:100px;margin-bottom:10px"">");
            signatureBlock.AppendLine($"\t\t<div style=\"background:white;margin-top:85px;margin-left:10px;width:fit-content;text-align:center\">{contactName}</div>");
            signatureBlock.AppendLine("\t</div>");

            if (!String.IsNullOrEmpty(title))
                signatureBlock.AppendLine(GetLabelValueHtml(Resources.PMResources.Common_Title, title));

            if (!string.IsNullOrEmpty(email))
                signatureBlock.AppendLine(GetLabelValueHtml(Resources.PMResources.Common_Email, email));

            if (!string.IsNullOrEmpty(phoneNumber))
                signatureBlock.AppendLine(GetLabelValueHtml(Resources.PMResources.Common_PhoneNumber, phoneNumber));
            signatureBlock.AppendLine("</div");

            return signatureBlock.ToString();
        }

        public static List<ReplaceableTag> SignatureTags
        {
            get
            {
                var tags = new List<ReplaceableTag>();
                tags.Add(new ReplaceableTag() { Tag = SignatureBlock, Title = SignatureBlock.SplitTagName() });
                tags.Add(new ReplaceableTag() { Tag = InitialsBlock, Title = InitialsBlock.SplitTagName() });
                return tags;
            }
        }

        public async Task<string> ReplaceCommonOrgTags(string template, Organization org, EntityHeader user)
        {
            var resource = await _mediaServices.GetResourceMediaAsync(org.DarkLogo.Id, org.ToEntityHeader(), user);

            var b64 = Convert.ToBase64String(resource.ImageBytes);
            template = template.Replace(OrganizationLogo.ToBracketedTag(), $"<img src=\"data:image/png;base64,{b64}\" />");
            template = template.Replace(CurrentDate.ToBracketedTag(), DateTime.Now.ToShortDateString());
            template = template.Replace(CurrentYear.ToBracketedTag(), DateTime.Now.Year.ToString());
            template = template.Replace(OrganizationName.ToBracketedTag(), org.Name);
            template = template.Replace(OrganizationWebSite.ToBracketedTag(), org.WebSite);
            template = template.Replace(PageBreak.ToBracketedTag(), "<hr attr-data='page-break' />");

            if (!EntityHeader.IsNullOrEmpty(org.PrimaryLocation))
            {
                var primaryLocation = await _orgLocationRepo.GetLocationAsync(org.PrimaryLocation.Id);
                template = template.Replace(OrganizationState.ToBracketedTag(), primaryLocation.StateProvince);
                template = template.Replace(OrganizationPhone.ToBracketedTag(), primaryLocation.PhoneNumber);
                template = template.Replace(OrganizationPrimaryLocation.ToBracketedTag(), @$"
<div>{primaryLocation.Addr1}</div>
<div>{primaryLocation.City}, {primaryLocation.StateProvince} {primaryLocation.PostalCode}</div>");
            }
            else
            {
                template = template.Replace(OrganizationBillingLocation.ToBracketedTag(), @$"No Primary Location");
            }

            if (!EntityHeader.IsNullOrEmpty(org.BillingLocation))
            {
                var billingLocation = await _orgLocationRepo.GetLocationAsync(org.PrimaryLocation.Id);
                template = template.Replace(OrganizationBillingLocation.ToBracketedTag(), @$"
<div>{billingLocation.Addr1}</div>
<div>{billingLocation.City}, {billingLocation.StateProvince} {billingLocation.PostalCode}</div>");
            }
            else
            {
                template = template.Replace(OrganizationBillingLocation.ToBracketedTag(), @$"No Billing Location");
            }


            return template;
        }

        public string ReplacePreviewOrgTags(string template)
        {
            template = template.Replace(CurrentDate.ToBracketedTag(), DateTime.Now.ToShortDateString());
            template = template.Replace(CurrentYear.ToBracketedTag(), DateTime.Now.Year.ToString());
            template = template.Replace(OrganizationLogo.ToBracketedTag(), "<img src=\"data:image/png;base64,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\" />" );
            template = template.Replace(OrganizationName.ToBracketedTag(), "Widgets are Us");
            template = template.Replace(OrganizationWebSite.ToBracketedTag(), "https://www.software-logistics.com");
            template = template.Replace(OrganizationPhone.ToBracketedTag(), "(727) 555-1212");
            template = template.Replace(OrganizationState.ToBracketedTag(), "FL");
            template = template.Replace(AccountManagerName.ToBracketedTag(), "Arthur AccountManager");
            template = template.Replace(BusinessDevelopmentRepName.ToBracketedTag(), "Betty BizRepDev");
            template = template.Replace(OrganizationPrimaryLocation.ToBracketedTag(), @"
<div>1313 Mocking Bird Drive</div>
<div>Springfield, OR 12345</div>");
            template = template.Replace(OrganizationBillingLocation.ToBracketedTag(), @"
<div>1313 Mocking Bird Drive</div>
<div>Springfield, OR 12345</div>"); 
            template = template.Replace(PageBreak.ToBracketedTag(), "<hr attr-data='page-break' />");
            return template;
        }

        public async Task<ListResponse<DocumentTemplateSummary>> GetDocumentTemplatesAsync(ListRequest listRequest, EntityHeader org, EntityHeader user)
        {
            return await _templateRepo.GetDocumentTemplatesAsync(org.Id, listRequest);
        }

        public async Task<ListResponse<DocumentTemplateSummary>> GetDocumentTemplatesAsync(DocumentTemplateTypes type, ListRequest listRequest, EntityHeader org, EntityHeader user)
        {
            return await _templateRepo.GetDocumentTemplatesAsync(type, org.Id, listRequest);
        }


        public async Task<InvokeResult> UpdateDocumentTemplateAsync(DocumentTemplate documentTemplate, EntityHeader org, EntityHeader user)
        {
            ValidationCheck(documentTemplate, Actions.Create);
            await AuthorizeAsync(documentTemplate, AuthorizeResult.AuthorizeActions.Update, user, org);
            await _templateRepo.UpdateDocumentTemplateAsync(documentTemplate);
            return InvokeResult.Success;
        }
    }
}

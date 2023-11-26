using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.Label_Title, AIResources.Names.Label_Help, AIResources.Names.Label_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources))]
    public class Label : EntityBase, IDescriptionEntity,IValidateable, IFormDescriptor
    {

        [FormField(LabelResource: AIResources.Names.Label_Title, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Title { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Icon, FieldType: FieldTypes.Icon, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Icon { get; set; }

        [FormField(LabelResource: AIResources.Names.Common_Description, FieldType: FieldTypes.MultiLineText, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Description { get; set; }

        public LabelSummary CreateSummary()
        {
            return new LabelSummary()
            {
                Id = Id,
                Name = Name,
                Key = Key,
                Description = Description,
            };
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(Key),
                nameof(Title),
                nameof(Icon),
                nameof(Description)
            };
        }
    }

    public class LabelSummary : SummaryData
    {

    }
}

// --- BEGIN CODE INDEX META (do not edit) ---
// ContentHash: 86547d6f738713fb7d84c8d18a04b320f54a6edf26b07d09e109c5501d55d6d1
// IndexVersion: 2
// --- END CODE INDEX META ---
using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace LagoVista.AI.Models
{
    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.Label_Title, AIResources.Names.Label_Help, AIResources.Names.Label_Description, EntityDescriptionAttribute.EntityTypes.SimpleModel, typeof(AIResources),
        FactoryUrl: "/api/ml/label/factory")]
    public class AiModelLabel : EntityBase,  IDescriptionEntity, IValidateable, IFormDescriptor, ITitledEntity, IIconEntity
    {
        public AiModelLabel()
        {

        }

    
        [FormField(LabelResource: AIResources.Names.Label_Title, FieldType: FieldTypes.Text, IsRequired: true, ResourceType: typeof(AIResources))]
        public string Title { get; set; }

        [FormField(LabelResource: AIResources.Names.Label_Icon, FieldType: FieldTypes.Icon, IsRequired: false, ResourceType: typeof(AIResources))]
        public string Icon { get; set; }
 
        public AiModelLabelSummary CreateSummary()
        {
            return new AiModelLabelSummary()
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

    public class AiModelLabelSummary : SummaryData
    {

    }
}

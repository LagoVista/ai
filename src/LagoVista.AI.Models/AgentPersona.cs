using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models;
using LagoVista.Core.Interfaces;
using LagoVista.Core.Validation;
using System.Collections.Generic;
using System;

namespace LagoVista.AI.Models
{
    /// <summary>
    /// Defines a user-selectable conversational persona configuration.
    /// This is intended to be orthogonal to agent/role/mode and should not change correctness contracts.
    /// </summary>
    /// 
    [EntityDescription(
        AIDomain.AIAdmin, AIResources.Names.AgentPersonaDefinition_Title, AIResources.Names.AgentPersonaDefinition_Help,
        AIResources.Names.AgentPersonaDefinition_Description, EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources),

        GetUrl: "/api/ai/agentpersona/{id}", GetListUrl: "/api/ai/agentpersonas", FactoryUrl: "/api/ai/agentpersona/factory", SaveUrl: "/api/ai/agentpersona",
        DeleteUrl: "/api/ai/agentpersona/{id}",

        ListUIUrl: "/mlworkbench/agentpersonas", EditUIUrl: "/mlworkbench/agentpersona/{id}", CreateUIUrl: "/mlworkbench/agentpersona/add",

        Icon: "icon-ae-call-center", ClusterKey: "prompting", ModelType: EntityDescriptionAttribute.ModelTypes.Configuration,
        Shape: EntityDescriptionAttribute.EntityShapes.Entity, Lifecycle: EntityDescriptionAttribute.Lifecycles.DesignTime,
        Sensitivity: EntityDescriptionAttribute.Sensitivities.Internal, IndexInclude: true, IndexTier: EntityDescriptionAttribute.IndexTiers.Secondary,
        IndexPriority: 70, IndexTagsCsv: "ai,prompting,configuration")]
    public partial class AgentPersonaDefinition : EntityBase, IValidateable, IFormDescriptor, IFormDescriptorCol2, ISummaryFactory
    {
        public const string ToneStyle_Neutral = "neutral";
        public const string ToneStyle_Conversational = "conversational";
        public const string ToneStyle_Warm = "warm";
        public const string ToneStyle_Direct = "direct";

        public const string VerbosityLevel_UltraConcise = "ultra-concise";
        public const string VerbosityLevel_Concise = "concise";
        public const string VerbosityLevel_Normal = "normal";
        public const string VerbosityLevel_Thorough = "thorough";

        public const string ReflectionLevel_None = "none";
        public const string ReflectionLevel_Light = "light";
        public const string ReflectionLevel_Normal = "normal";

        public const string SuggestionStyle_ReactiveOnly = "reactive-only";
        public const string SuggestionStyle_OfferOptions = "offer-options";
        public const string SuggestionStyle_Proactive = "proactive";

        public const string ConfirmationStrictness_Low = "low";
        public const string ConfirmationStrictness_Normal = "normal";
        public const string ConfirmationStrictness_High = "high";

        public const string HumorLevel_Off = "off";
        public const string HumorLevel_Light = "light";

        public const string ChallengeLevel_None = "none";
        public const string ChallengeLevel_Light = "light";
        public const string ChallengeLevel_Normal = "normal";
        public const string ChallengeLevel_High = "high";
        public const string ChallengeLevel_Adversarial = "adversarial";

        public const string CreativityLevel_Minimal = "minimal";
        public const string CreativityLevel_Constrained = "constrained";
        public const string CreativityLevel_Balanced = "balanced";
        public const string CreativityLevel_Expansive = "expansive";

        public const string AssumptionTolerance_Low = "low";
        public const string AssumptionTolerance_Normal = "normal";
        public const string AssumptionTolerance_High = "high";

        public const string DetailFocus_Outcome = "outcome";
        public const string DetailFocus_Balanced = "balanced";
        public const string DetailFocus_Process = "process";

        public const string RiskSensitivity_Low = "low";
        public const string RiskSensitivity_Normal = "normal";
        public const string RiskSensitivity_High = "high";


        [FormField(LabelResource: AIResources.Names.Common_Icon, FieldType: FieldTypes.Icon, ResourceType: typeof(AIResources))]
        public string Icon { get; set; } = "icon-ae-call-center";

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_DisplayName,
            FieldType: FieldTypes.Text,
            ResourceType: typeof(AIResources),
            IsRequired: true,
            IsUserEditable: true)]
        public string DisplayName { get; set; }

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_ToneStyle,
            FieldType: FieldTypes.Picker,
            ResourceType: typeof(AIResources),
            EnumType: (typeof(ToneStyle)),
            IsRequired: true,
            IsUserEditable: true)]
        public EntityHeader<ToneStyle> ToneStyle { get; set; } = EntityHeader<ToneStyle>.Create(Models.ToneStyle.Neutral);

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_VerbosityLevel,
            FieldType: FieldTypes.Picker,
            EnumType: (typeof(VerbosityLevel)),
            ResourceType: typeof(AIResources),
            IsRequired: true,
            IsUserEditable: true)]
        public EntityHeader<VerbosityLevel> VerbosityLevel { get; set; } = EntityHeader<VerbosityLevel>.Create(Models.VerbosityLevel.Concise);

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_ReflectionLevel,
            FieldType: FieldTypes.Picker,
            EnumType: (typeof(ReflectionLevel)),
            ResourceType: typeof(AIResources),
            IsRequired: true,
            IsUserEditable: true)]
        public EntityHeader<ReflectionLevel> ReflectionLevel { get; set; } = EntityHeader<ReflectionLevel>.Create(Models.ReflectionLevel.Light);

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_SuggestionStyle,
            FieldType: FieldTypes.Picker,
            EnumType: (typeof(SuggestionStyle)),
            ResourceType: typeof(AIResources),
            IsRequired: true,
            IsUserEditable: true)]
        public EntityHeader<SuggestionStyle> SuggestionStyle { get; set; } = EntityHeader<SuggestionStyle>.Create(Models.SuggestionStyle.OfferOptions);

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_ConfirmationStrictness,
            FieldType: FieldTypes.Picker,
            EnumType: (typeof(ConfirmationStrictness)),
            ResourceType: typeof(AIResources),
            IsRequired: true,
            IsUserEditable: true)]
        public EntityHeader<ConfirmationStrictness> ConfirmationStrictness { get; set; } = EntityHeader<ConfirmationStrictness>.Create(Models.ConfirmationStrictness.Normal);

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_HumorLevel,
            FieldType: FieldTypes.Picker,
            EnumType:(typeof(HumorLevel)),
            ResourceType: typeof(AIResources),
            IsRequired: true,
            IsUserEditable: true)]
        public EntityHeader<HumorLevel> HumorLevel { get; set; } = EntityHeader<HumorLevel>.Create(Models.HumorLevel.Off);

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_AdditionalConfiguration,
            FieldType: FieldTypes.MultiLineText,
            ResourceType: typeof(AIResources),
            IsRequired: false,
            IsUserEditable: true)]
        public string AdditionalConfiguration { get; set; }

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_ChallengeLevel,
            FieldType: FieldTypes.Picker,
            ResourceType: typeof(AIResources),
            IsRequired: true,
            EnumType: typeof(ChallengeLevel),
            IsUserEditable: true)]
        public EntityHeader<ChallengeLevel> ChallengeLevel { get; set; } = EntityHeader<ChallengeLevel>.Create(Models.ChallengeLevel.Normal);

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_CreativityLevel,
            FieldType: FieldTypes.Picker,
            ResourceType: typeof(AIResources),
            IsRequired: true,
            EnumType:(typeof(CreativityLevel)),
            IsUserEditable: true)]
        public EntityHeader<CreativityLevel> CreativityLevel { get; set; } = EntityHeader<CreativityLevel>.Create(Models.CreativityLevel.Balanced); 

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_AssumptionTolerance,
            FieldType: FieldTypes.Picker,
            ResourceType: typeof(AIResources),
            IsRequired: true,
            EnumType:typeof(AssumptionTolerance),
            IsUserEditable: true)]
        public EntityHeader<AssumptionTolerance> AssumptionTolerance { get; set; } = EntityHeader<AssumptionTolerance>.Create(Models.AssumptionTolerance.Normal);

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_DetailFocus,
            FieldType: FieldTypes.Picker,
            ResourceType: typeof(AIResources),
            IsRequired: true,
            EnumType:typeof(DetailFocus),
            IsUserEditable: true)]
        public EntityHeader<DetailFocus> DetailFocus { get; set; } = EntityHeader<DetailFocus>.Create(Models.DetailFocus.Balanced);

        public AgentPersonaDefinitionSummary CreateSummary()
        {
            var summary = new AgentPersonaDefinitionSummary();
            summary.Populate(this);
            return summary;
        }

        [FormField(
            LabelResource: AIResources.Names.PersonaDefinition_RiskSensitivity,
            FieldType: FieldTypes.Picker,
            ResourceType: typeof(AIResources),
            IsRequired: true,
            EnumType:typeof(RiskSensitivity),
            IsUserEditable: true)]
        public EntityHeader<RiskSensitivity> RiskSensitivity { get; set; } = EntityHeader<RiskSensitivity>.Create(Models.RiskSensitivity.Normal);

        /// <summary>
        /// Optional: a compact “persona header” you can inject into prompts at runtime.
        /// Keep this non-normative; treat it as preference.
        /// </summary>
        public string BuildPersonaHeader()
        {
            return $"persona:{DisplayName}; tone:{ToneStyle.Value}; verbosity:{VerbosityLevel.Value}; reflection:{ReflectionLevel.Value}; suggestions:{SuggestionStyle.Value}; confirm:{ConfirmationStrictness.Value}; humor:{HumorLevel.Value}";
        }

        public List<string> GetFormFields()
        {
            return new List<string>()
            {
                nameof(Name),
                nameof(DisplayName),
                nameof(Key),
                nameof(Icon),
                nameof(Description),
                nameof(AdditionalConfiguration),
            };
        }

        public List<string> GetFormFieldsCol2()
        {
            return new List<string>()
            {
                nameof(ToneStyle),
                nameof(ChallengeLevel),
                nameof(CreativityLevel),
                nameof(VerbosityLevel),
                nameof(ReflectionLevel),
                nameof(SuggestionStyle),
                nameof(ConfirmationStrictness),
                nameof(HumorLevel),
                nameof(AssumptionTolerance),
                nameof(DetailFocus),
                nameof(RiskSensitivity),
            };
        }

        ISummaryData ISummaryFactory.CreateSummary()
        {
            return this.CreateSummary();
        }
    }

    [EntityDescription(AIDomain.AIAdmin, AIResources.Names.AgentPersonaDefinition_Title, AIResources.Names.AgentPersonaDefinition_Help, AIResources.Names.AgentPersonaDefinition_Description, EntityDescriptionAttribute.EntityTypes.CoreIoTModel, typeof(AIResources),
        GetUrl: "/api/ai/agentpersona/{id}", GetListUrl: "/api/ai/agentpersonas", FactoryUrl: "/api/ai/agentpersona/factory", SaveUrl: "/api/ai/agentpersona", DeleteUrl: "/api/ai/agentpersona/{id}",
        ListUIUrl: "/mlworkbench/agentpersonas", EditUIUrl: "/mlworkbench/agentpersona/{id}", CreateUIUrl: "/mlworkbench/agentpersona/add", Icon: "icon-ae-call-center")]
    public class AgentPersonaDefinitionSummary : SummaryData
    {

    }

    public enum ToneStyle
    {
        [EnumLabel(AgentPersonaDefinition.ToneStyle_Neutral, AIResources.Names.ToneStyle_Neutral, typeof(AIResources))]
        Neutral,

        [EnumLabel(AgentPersonaDefinition.ToneStyle_Conversational, AIResources.Names.ToneStyle_Conversational, typeof(AIResources))]
        Conversational,

        [EnumLabel(AgentPersonaDefinition.ToneStyle_Warm, AIResources.Names.ToneStyle_Warm, typeof(AIResources))]
        Warm,

        [EnumLabel(AgentPersonaDefinition.ToneStyle_Direct, AIResources.Names.ToneStyle_Direct, typeof(AIResources))]
        Direct
    }

    public enum VerbosityLevel
    {
        [EnumLabel(AgentPersonaDefinition.VerbosityLevel_UltraConcise, AIResources.Names.VerbosityLevel_UltraConcise, typeof(AIResources))]
        UltraConcise,

        [EnumLabel(AgentPersonaDefinition.VerbosityLevel_Concise, AIResources.Names.VerbosityLevel_Concise, typeof(AIResources))]
        Concise,

        [EnumLabel(AgentPersonaDefinition.VerbosityLevel_Normal, AIResources.Names.VerbosityLevel_Normal, typeof(AIResources))]
        Normal,

        [EnumLabel(AgentPersonaDefinition.VerbosityLevel_Thorough, AIResources.Names.VerbosityLevel_Thorough, typeof(AIResources))]
        Thorough
    }

    public enum ReflectionLevel
    {
        [EnumLabel(AgentPersonaDefinition.ReflectionLevel_None, AIResources.Names.ReflectionLevel_None, typeof(AIResources))]
        None,

        [EnumLabel(AgentPersonaDefinition.ReflectionLevel_Light, AIResources.Names.ReflectionLevel_Light, typeof(AIResources))]
        Light,

        [EnumLabel(AgentPersonaDefinition.ReflectionLevel_Normal, AIResources.Names.ReflectionLevel_Normal, typeof(AIResources))]
        Normal
    }

    public enum SuggestionStyle
    {
        [EnumLabel(AgentPersonaDefinition.SuggestionStyle_ReactiveOnly, AIResources.Names.SuggestionStyle_ReactiveOnly, typeof(AIResources))]
        ReactiveOnly,

        [EnumLabel(AgentPersonaDefinition.SuggestionStyle_OfferOptions, AIResources.Names.SuggestionStyle_OfferOptions, typeof(AIResources))]
        OfferOptions,

        [EnumLabel(AgentPersonaDefinition.SuggestionStyle_Proactive, AIResources.Names.SuggestionStyle_Proactive, typeof(AIResources))]
        Proactive
    }

    public enum ConfirmationStrictness
    {
        [EnumLabel(AgentPersonaDefinition.ConfirmationStrictness_Low, AIResources.Names.ConfirmationStrictness_Low, typeof(AIResources))]
        Low,

        [EnumLabel(AgentPersonaDefinition.ConfirmationStrictness_Normal, AIResources.Names.ConfirmationStrictness_Normal, typeof(AIResources))]
        Normal,

        [EnumLabel(AgentPersonaDefinition.ConfirmationStrictness_High, AIResources.Names.ConfirmationStrictness_High, typeof(AIResources))]
        High
    }

    public enum HumorLevel
    {
        [EnumLabel(AgentPersonaDefinition.HumorLevel_Off, AIResources.Names.HumorLevel_Off, typeof(AIResources))]
        Off,

        [EnumLabel(AgentPersonaDefinition.HumorLevel_Light, AIResources.Names.HumorLevel_Light, typeof(AIResources))]
        Light
    }

    public enum ChallengeLevel
    {
        [EnumLabel(AgentPersonaDefinition.ChallengeLevel_None, AIResources.Names.ChallengeLevel_None, typeof(AIResources))]
        None,

        [EnumLabel(AgentPersonaDefinition.ChallengeLevel_Light, AIResources.Names.ChallengeLevel_Light, typeof(AIResources))]
        Light,

        [EnumLabel(AgentPersonaDefinition.ChallengeLevel_Normal, AIResources.Names.ChallengeLevel_Normal, typeof(AIResources))]
        Normal,

        [EnumLabel(AgentPersonaDefinition.ChallengeLevel_High, AIResources.Names.ChallengeLevel_High, typeof(AIResources))]
        High,
        [EnumLabel(AgentPersonaDefinition.ChallengeLevel_Adversarial, AIResources.Names.ChallengeLevel_Adversarial, typeof(AIResources))]
        Adversarial
    }

    public enum CreativityLevel
    {
        [EnumLabel(AgentPersonaDefinition.CreativityLevel_Minimal, AIResources.Names.CreativityLevel_Minimal, typeof(AIResources))]
        Minimal,

        [EnumLabel(AgentPersonaDefinition.CreativityLevel_Constrained, AIResources.Names.CreativityLevel_Constrained, typeof(AIResources))]
        Constrained,

        [EnumLabel(AgentPersonaDefinition.CreativityLevel_Balanced, AIResources.Names.CreativityLevel_Balanced, typeof(AIResources))]
        Balanced,

        [EnumLabel(AgentPersonaDefinition.CreativityLevel_Expansive, AIResources.Names.CreativityLevel_Expansive, typeof(AIResources))]
        Expansive
    }

    public enum AssumptionTolerance
    {
        [EnumLabel(AgentPersonaDefinition.AssumptionTolerance_Low, AIResources.Names.AssumptionTolerance_Low, typeof(AIResources))]
        Low,

        [EnumLabel(AgentPersonaDefinition.AssumptionTolerance_Normal, AIResources.Names.AssumptionTolerance_Normal, typeof(AIResources))]
        Normal,

        [EnumLabel(AgentPersonaDefinition.AssumptionTolerance_High, AIResources.Names.AssumptionTolerance_High, typeof(AIResources))]
        High
    }


    public enum DetailFocus
    {
        [EnumLabel(AgentPersonaDefinition.DetailFocus_Outcome, AIResources.Names.DetailFocus_Outcome, typeof(AIResources))]
        Outcome,

        [EnumLabel(AgentPersonaDefinition.DetailFocus_Balanced, AIResources.Names.DetailFocus_Balanced, typeof(AIResources))]
        Balanced,

        [EnumLabel(AgentPersonaDefinition.DetailFocus_Process, AIResources.Names.DetailFocus_Process, typeof(AIResources))]
        Process
    }

    public enum RiskSensitivity
    {
        [EnumLabel(AgentPersonaDefinition.RiskSensitivity_Low, AIResources.Names.RiskSensitivity_Low, typeof(AIResources))]
        Low,

        [EnumLabel(AgentPersonaDefinition.RiskSensitivity_Normal, AIResources.Names.RiskSensitivity_Normal, typeof(AIResources))]
        Normal,

        [EnumLabel(AgentPersonaDefinition.RiskSensitivity_High, AIResources.Names.RiskSensitivity_High, typeof(AIResources))]
        High
    }


}

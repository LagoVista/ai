using LagoVista.AI.Models.Resources;
using LagoVista.Core.Attributes;
using LagoVista.Core.Models;

namespace LagoVista.AI.Models
{
    using System;

    namespace LagoVista.AI.Models.Personas
    {
        /// <summary>
        /// Defines a user-selectable conversational persona configuration.
        /// This is intended to be orthogonal to agent/role/mode and should not change correctness contracts.
        /// </summary>
        public class PersonaDefinition : EntityBase
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
                IsRequired: true,
                IsUserEditable: true)]
            public ToneStyle ToneStyle { get; set; } = ToneStyle.Neutral;

            [FormField(
                LabelResource: AIResources.Names.PersonaDefinition_VerbosityLevel,
                FieldType: FieldTypes.Picker,
                ResourceType: typeof(AIResources),
                IsRequired: true,
                IsUserEditable: true)]
            public VerbosityLevel VerbosityLevel { get; set; } = VerbosityLevel.Concise;

            [FormField(
                LabelResource: AIResources.Names.PersonaDefinition_ReflectionLevel,
                FieldType: FieldTypes.Picker,
                ResourceType: typeof(AIResources),
                IsRequired: true,
                IsUserEditable: true)]
            public ReflectionLevel ReflectionLevel { get; set; } = ReflectionLevel.Light;

            [FormField(
                LabelResource: AIResources.Names.PersonaDefinition_SuggestionStyle,
                FieldType: FieldTypes.Picker,
                ResourceType: typeof(AIResources),
                IsRequired: true,
                IsUserEditable: true)]
            public SuggestionStyle SuggestionStyle { get; set; } = SuggestionStyle.OfferOptions;

            [FormField(
                LabelResource: AIResources.Names.PersonaDefinition_ConfirmationStrictness,
                FieldType: FieldTypes.Picker,
                ResourceType: typeof(AIResources),
                IsRequired: true,
                IsUserEditable: true)]
            public ConfirmationStrictness ConfirmationStrictness { get; set; } = ConfirmationStrictness.Normal;

            [FormField(
                LabelResource: AIResources.Names.PersonaDefinition_HumorLevel,
                FieldType: FieldTypes.Picker,
                ResourceType: typeof(AIResources),
                IsRequired: true,
                IsUserEditable: true)]
            public HumorLevel HumorLevel { get; set; } = HumorLevel.Off;

            [FormField(
                LabelResource: AIResources.Names.PersonaDefinition_Notes,
                FieldType: FieldTypes.MultiLineText,
                ResourceType: typeof(AIResources),
                IsRequired: false,
                IsUserEditable: true)]
            public string Notes { get; set; }

            /// <summary>
            /// Optional: a compact “persona header” you can inject into prompts at runtime.
            /// Keep this non-normative; treat it as preference.
            /// </summary>
            public string BuildPersonaHeader()
            {
                return $"persona:{DisplayName}; tone:{ToneStyle}; verbosity:{VerbosityLevel}; reflection:{ReflectionLevel}; suggestions:{SuggestionStyle}; confirm:{ConfirmationStrictness}; humor:{HumorLevel}";
            }
        }

        public enum ToneStyle
        {
            [EnumLabel(PersonaDefinition.ToneStyle_Neutral, AIResources.Names.ToneStyle_Neutral, typeof(AIResources))]
            Neutral,

            [EnumLabel(PersonaDefinition.ToneStyle_Conversational, AIResources.Names.ToneStyle_Conversational, typeof(AIResources))]
            Conversational,

            [EnumLabel(PersonaDefinition.ToneStyle_Warm, AIResources.Names.ToneStyle_Warm, typeof(AIResources))]
            Warm,

            [EnumLabel(PersonaDefinition.ToneStyle_Direct, AIResources.Names.ToneStyle_Direct, typeof(AIResources))]
            Direct
        }

        public enum VerbosityLevel
        {
            [EnumLabel(PersonaDefinition.VerbosityLevel_UltraConcise, AIResources.Names.VerbosityLevel_UltraConcise, typeof(AIResources))]
            UltraConcise,

            [EnumLabel(PersonaDefinition.VerbosityLevel_Concise, AIResources.Names.VerbosityLevel_Concise, typeof(AIResources))]
            Concise,

            [EnumLabel(PersonaDefinition.VerbosityLevel_Normal, AIResources.Names.VerbosityLevel_Normal, typeof(AIResources))]
            Normal,

            [EnumLabel(PersonaDefinition.VerbosityLevel_Thorough, AIResources.Names.VerbosityLevel_Thorough, typeof(AIResources))]
            Thorough
        }

        public enum ReflectionLevel
        {
            [EnumLabel(PersonaDefinition.ReflectionLevel_None, AIResources.Names.ReflectionLevel_None, typeof(AIResources))]
            None,

            [EnumLabel(PersonaDefinition.ReflectionLevel_Light, AIResources.Names.ReflectionLevel_Light, typeof(AIResources))]
            Light,

            [EnumLabel(PersonaDefinition.ReflectionLevel_Normal, AIResources.Names.ReflectionLevel_Normal, typeof(AIResources))]
            Normal
        }

        public enum SuggestionStyle
        {
            [EnumLabel(PersonaDefinition.SuggestionStyle_ReactiveOnly, AIResources.Names.SuggestionStyle_ReactiveOnly, typeof(AIResources))]
            ReactiveOnly,

            [EnumLabel(PersonaDefinition.SuggestionStyle_OfferOptions, AIResources.Names.SuggestionStyle_OfferOptions, typeof(AIResources))]
            OfferOptions,

            [EnumLabel(PersonaDefinition.SuggestionStyle_Proactive, AIResources.Names.SuggestionStyle_Proactive, typeof(AIResources))]
            Proactive
        }

        public enum ConfirmationStrictness
        {
            [EnumLabel(PersonaDefinition.ConfirmationStrictness_Low, AIResources.Names.ConfirmationStrictness_Low, typeof(AIResources))]
            Low,

            [EnumLabel(PersonaDefinition.ConfirmationStrictness_Normal, AIResources.Names.ConfirmationStrictness_Normal, typeof(AIResources))]
            Normal,

            [EnumLabel(PersonaDefinition.ConfirmationStrictness_High, AIResources.Names.ConfirmationStrictness_High, typeof(AIResources))]
            High
        }

        public enum HumorLevel
        {
            [EnumLabel(PersonaDefinition.HumorLevel_Off, AIResources.Names.HumorLevel_Off, typeof(AIResources))]
            Off,

            [EnumLabel(PersonaDefinition.HumorLevel_Light, AIResources.Names.HumorLevel_Light, typeof(AIResources))]
            Light
        }
    }
}

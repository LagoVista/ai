using System.Globalization;
using System.Reflection;  

//Resources:AIResources:Common_Datestamp
namespace LagoVista.AI.Models.Resources
{
	public class AIResources
	{
        private static global::System.Resources.ResourceManager _resourceManager;
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        private static global::System.Resources.ResourceManager ResourceManager 
		{
            get 
			{
                if (object.ReferenceEquals(_resourceManager, null)) 
				{
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("LagoVista.AI.Models.Resources.AIResources", typeof(AIResources).GetTypeInfo().Assembly);
                    _resourceManager = temp;
                }
                return _resourceManager;
            }
        }
        
        /// <summary>
        ///   Returns the formatted resource string.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        private static string GetResourceString(string key, params string[] tokens)
		{
			var culture = CultureInfo.CurrentCulture;;
            var str = ResourceManager.GetString(key, culture);

			for(int i = 0; i < tokens.Length; i += 2)
				str = str.Replace(tokens[i], tokens[i+1]);
										
            return str;
        }
        
        /// <summary>
        ///   Returns the formatted resource string.
        /// </summary>
		/*
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        private static HtmlString GetResourceHtmlString(string key, params string[] tokens)
		{
			var str = GetResourceString(key, tokens);
							
			if(str.StartsWith("HTML:"))
				str = str.Substring(5);

			return new HtmlString(str);
        }*/
		
		public static string Common_Datestamp { get { return GetResourceString("Common_Datestamp"); } }
//Resources:AIResources:Common_Description

		public static string Common_Description { get { return GetResourceString("Common_Description"); } }
//Resources:AIResources:Common_Key

		public static string Common_Key { get { return GetResourceString("Common_Key"); } }
//Resources:AIResources:Common_Key_Help

		public static string Common_Key_Help { get { return GetResourceString("Common_Key_Help"); } }
//Resources:AIResources:Common_Key_Validation

		public static string Common_Key_Validation { get { return GetResourceString("Common_Key_Validation"); } }
//Resources:AIResources:Common_Name

		public static string Common_Name { get { return GetResourceString("Common_Name"); } }
//Resources:AIResources:Common_Notes

		public static string Common_Notes { get { return GetResourceString("Common_Notes"); } }
//Resources:AIResources:Experiemnt_Help

		public static string Experiemnt_Help { get { return GetResourceString("Experiemnt_Help"); } }
//Resources:AIResources:Experiment_Description

		public static string Experiment_Description { get { return GetResourceString("Experiment_Description"); } }
//Resources:AIResources:Experiment_Instructions

		public static string Experiment_Instructions { get { return GetResourceString("Experiment_Instructions"); } }
//Resources:AIResources:Experiment_Title

		public static string Experiment_Title { get { return GetResourceString("Experiment_Title"); } }
//Resources:AIResources:Label_Description

		public static string Label_Description { get { return GetResourceString("Label_Description"); } }
//Resources:AIResources:Label_Help

		public static string Label_Help { get { return GetResourceString("Label_Help"); } }
//Resources:AIResources:Label_Icon

		public static string Label_Icon { get { return GetResourceString("Label_Icon"); } }
//Resources:AIResources:Label_Index

		public static string Label_Index { get { return GetResourceString("Label_Index"); } }
//Resources:AIResources:Label_Key

		public static string Label_Key { get { return GetResourceString("Label_Key"); } }
//Resources:AIResources:Label_Title

		public static string Label_Title { get { return GetResourceString("Label_Title"); } }
//Resources:AIResources:Model_Description

		public static string Model_Description { get { return GetResourceString("Model_Description"); } }
//Resources:AIResources:Model_Experiments

		public static string Model_Experiments { get { return GetResourceString("Model_Experiments"); } }
//Resources:AIResources:Model_Help

		public static string Model_Help { get { return GetResourceString("Model_Help"); } }
//Resources:AIResources:Model_ModelCategory

		public static string Model_ModelCategory { get { return GetResourceString("Model_ModelCategory"); } }
//Resources:AIResources:Model_ModelCategory_Select

		public static string Model_ModelCategory_Select { get { return GetResourceString("Model_ModelCategory_Select"); } }
//Resources:AIResources:Model_ModelType

		public static string Model_ModelType { get { return GetResourceString("Model_ModelType"); } }
//Resources:AIResources:Model_Revisions

		public static string Model_Revisions { get { return GetResourceString("Model_Revisions"); } }
//Resources:AIResources:Model_Title

		public static string Model_Title { get { return GetResourceString("Model_Title"); } }
//Resources:AIResources:Model_Type

		public static string Model_Type { get { return GetResourceString("Model_Type"); } }
//Resources:AIResources:Model_Type_PyTorch

		public static string Model_Type_PyTorch { get { return GetResourceString("Model_Type_PyTorch"); } }
//Resources:AIResources:Model_Type_Select

		public static string Model_Type_Select { get { return GetResourceString("Model_Type_Select"); } }
//Resources:AIResources:Model_Type_TensorFlow

		public static string Model_Type_TensorFlow { get { return GetResourceString("Model_Type_TensorFlow"); } }
//Resources:AIResources:Model_Type_TensorFlow_Lite

		public static string Model_Type_TensorFlow_Lite { get { return GetResourceString("Model_Type_TensorFlow_Lite"); } }
//Resources:AIResources:ModelCategory_Description

		public static string ModelCategory_Description { get { return GetResourceString("ModelCategory_Description"); } }
//Resources:AIResources:ModelCategory_Help

		public static string ModelCategory_Help { get { return GetResourceString("ModelCategory_Help"); } }
//Resources:AIResources:ModelCategory_Title

		public static string ModelCategory_Title { get { return GetResourceString("ModelCategory_Title"); } }
//Resources:AIResources:ModelNotes_AddedBy

		public static string ModelNotes_AddedBy { get { return GetResourceString("ModelNotes_AddedBy"); } }
//Resources:AIResources:ModelNotes_Description

		public static string ModelNotes_Description { get { return GetResourceString("ModelNotes_Description"); } }
//Resources:AIResources:ModelNotes_Help

		public static string ModelNotes_Help { get { return GetResourceString("ModelNotes_Help"); } }
//Resources:AIResources:ModelNotes_Note

		public static string ModelNotes_Note { get { return GetResourceString("ModelNotes_Note"); } }
//Resources:AIResources:ModelNotes_Title

		public static string ModelNotes_Title { get { return GetResourceString("ModelNotes_Title"); } }
//Resources:AIResources:ModelRevision_Configuration

		public static string ModelRevision_Configuration { get { return GetResourceString("ModelRevision_Configuration"); } }
//Resources:AIResources:ModelRevision_DateStamp

		public static string ModelRevision_DateStamp { get { return GetResourceString("ModelRevision_DateStamp"); } }
//Resources:AIResources:ModelRevision_Description

		public static string ModelRevision_Description { get { return GetResourceString("ModelRevision_Description"); } }
//Resources:AIResources:ModelRevision_Help

		public static string ModelRevision_Help { get { return GetResourceString("ModelRevision_Help"); } }
//Resources:AIResources:ModelRevision_Labels

		public static string ModelRevision_Labels { get { return GetResourceString("ModelRevision_Labels"); } }
//Resources:AIResources:ModelRevision_Minor_Version_Number

		public static string ModelRevision_Minor_Version_Number { get { return GetResourceString("ModelRevision_Minor_Version_Number"); } }
//Resources:AIResources:ModelRevision_Notes

		public static string ModelRevision_Notes { get { return GetResourceString("ModelRevision_Notes"); } }
//Resources:AIResources:ModelRevision_Preprocessors

		public static string ModelRevision_Preprocessors { get { return GetResourceString("ModelRevision_Preprocessors"); } }
//Resources:AIResources:ModelRevision_Quality

		public static string ModelRevision_Quality { get { return GetResourceString("ModelRevision_Quality"); } }
//Resources:AIResources:ModelRevision_Quality_Excellent

		public static string ModelRevision_Quality_Excellent { get { return GetResourceString("ModelRevision_Quality_Excellent"); } }
//Resources:AIResources:ModelRevision_Quality_Good

		public static string ModelRevision_Quality_Good { get { return GetResourceString("ModelRevision_Quality_Good"); } }
//Resources:AIResources:ModelRevision_Quality_Medium

		public static string ModelRevision_Quality_Medium { get { return GetResourceString("ModelRevision_Quality_Medium"); } }
//Resources:AIResources:ModelRevision_Quality_Poor

		public static string ModelRevision_Quality_Poor { get { return GetResourceString("ModelRevision_Quality_Poor"); } }
//Resources:AIResources:ModelRevision_Quality_Select

		public static string ModelRevision_Quality_Select { get { return GetResourceString("ModelRevision_Quality_Select"); } }
//Resources:AIResources:ModelRevision_Quality_Unknown

		public static string ModelRevision_Quality_Unknown { get { return GetResourceString("ModelRevision_Quality_Unknown"); } }
//Resources:AIResources:ModelRevision_Settings

		public static string ModelRevision_Settings { get { return GetResourceString("ModelRevision_Settings"); } }
//Resources:AIResources:ModelRevision_Status

		public static string ModelRevision_Status { get { return GetResourceString("ModelRevision_Status"); } }
//Resources:AIResources:ModelRevision_Status_Alpha

		public static string ModelRevision_Status_Alpha { get { return GetResourceString("ModelRevision_Status_Alpha"); } }
//Resources:AIResources:ModelRevision_Status_Beta

		public static string ModelRevision_Status_Beta { get { return GetResourceString("ModelRevision_Status_Beta"); } }
//Resources:AIResources:ModelRevision_Status_Experimental

		public static string ModelRevision_Status_Experimental { get { return GetResourceString("ModelRevision_Status_Experimental"); } }
//Resources:AIResources:ModelRevision_Status_New

		public static string ModelRevision_Status_New { get { return GetResourceString("ModelRevision_Status_New"); } }
//Resources:AIResources:ModelRevision_Status_Obsolete

		public static string ModelRevision_Status_Obsolete { get { return GetResourceString("ModelRevision_Status_Obsolete"); } }
//Resources:AIResources:ModelRevision_Status_Production

		public static string ModelRevision_Status_Production { get { return GetResourceString("ModelRevision_Status_Production"); } }
//Resources:AIResources:ModelRevision_Status_Select

		public static string ModelRevision_Status_Select { get { return GetResourceString("ModelRevision_Status_Select"); } }
//Resources:AIResources:ModelRevision_Title

		public static string ModelRevision_Title { get { return GetResourceString("ModelRevision_Title"); } }
//Resources:AIResources:ModelRevision_TrainingSettings

		public static string ModelRevision_TrainingSettings { get { return GetResourceString("ModelRevision_TrainingSettings"); } }
//Resources:AIResources:ModelRevision_Version_Number

		public static string ModelRevision_Version_Number { get { return GetResourceString("ModelRevision_Version_Number"); } }
//Resources:AIResources:ModelSetting_Description

		public static string ModelSetting_Description { get { return GetResourceString("ModelSetting_Description"); } }
//Resources:AIResources:ModelSetting_Help

		public static string ModelSetting_Help { get { return GetResourceString("ModelSetting_Help"); } }
//Resources:AIResources:ModelSetting_Title

		public static string ModelSetting_Title { get { return GetResourceString("ModelSetting_Title"); } }
//Resources:AIResources:ModelSetting_Value

		public static string ModelSetting_Value { get { return GetResourceString("ModelSetting_Value"); } }
//Resources:AIResources:Preprocessor_ClassName

		public static string Preprocessor_ClassName { get { return GetResourceString("Preprocessor_ClassName"); } }
//Resources:AIResources:Preprocessor_Description

		public static string Preprocessor_Description { get { return GetResourceString("Preprocessor_Description"); } }
//Resources:AIResources:Preprocessor_Help

		public static string Preprocessor_Help { get { return GetResourceString("Preprocessor_Help"); } }
//Resources:AIResources:Preprocessor_Settings

		public static string Preprocessor_Settings { get { return GetResourceString("Preprocessor_Settings"); } }
//Resources:AIResources:Preprocessor_Title

		public static string Preprocessor_Title { get { return GetResourceString("Preprocessor_Title"); } }
//Resources:AIResources:PreprocessorSetting_Description

		public static string PreprocessorSetting_Description { get { return GetResourceString("PreprocessorSetting_Description"); } }
//Resources:AIResources:PreprocessorSetting_Help

		public static string PreprocessorSetting_Help { get { return GetResourceString("PreprocessorSetting_Help"); } }
//Resources:AIResources:PreprocessorSetting_Title

		public static string PreprocessorSetting_Title { get { return GetResourceString("PreprocessorSetting_Title"); } }
//Resources:AIResources:PreprocessorSetting_Value

		public static string PreprocessorSetting_Value { get { return GetResourceString("PreprocessorSetting_Value"); } }

		public static class Names
		{
			public const string Common_Datestamp = "Common_Datestamp";
			public const string Common_Description = "Common_Description";
			public const string Common_Key = "Common_Key";
			public const string Common_Key_Help = "Common_Key_Help";
			public const string Common_Key_Validation = "Common_Key_Validation";
			public const string Common_Name = "Common_Name";
			public const string Common_Notes = "Common_Notes";
			public const string Experiemnt_Help = "Experiemnt_Help";
			public const string Experiment_Description = "Experiment_Description";
			public const string Experiment_Instructions = "Experiment_Instructions";
			public const string Experiment_Title = "Experiment_Title";
			public const string Label_Description = "Label_Description";
			public const string Label_Help = "Label_Help";
			public const string Label_Icon = "Label_Icon";
			public const string Label_Index = "Label_Index";
			public const string Label_Key = "Label_Key";
			public const string Label_Title = "Label_Title";
			public const string Model_Description = "Model_Description";
			public const string Model_Experiments = "Model_Experiments";
			public const string Model_Help = "Model_Help";
			public const string Model_ModelCategory = "Model_ModelCategory";
			public const string Model_ModelCategory_Select = "Model_ModelCategory_Select";
			public const string Model_ModelType = "Model_ModelType";
			public const string Model_Revisions = "Model_Revisions";
			public const string Model_Title = "Model_Title";
			public const string Model_Type = "Model_Type";
			public const string Model_Type_PyTorch = "Model_Type_PyTorch";
			public const string Model_Type_Select = "Model_Type_Select";
			public const string Model_Type_TensorFlow = "Model_Type_TensorFlow";
			public const string Model_Type_TensorFlow_Lite = "Model_Type_TensorFlow_Lite";
			public const string ModelCategory_Description = "ModelCategory_Description";
			public const string ModelCategory_Help = "ModelCategory_Help";
			public const string ModelCategory_Title = "ModelCategory_Title";
			public const string ModelNotes_AddedBy = "ModelNotes_AddedBy";
			public const string ModelNotes_Description = "ModelNotes_Description";
			public const string ModelNotes_Help = "ModelNotes_Help";
			public const string ModelNotes_Note = "ModelNotes_Note";
			public const string ModelNotes_Title = "ModelNotes_Title";
			public const string ModelRevision_Configuration = "ModelRevision_Configuration";
			public const string ModelRevision_DateStamp = "ModelRevision_DateStamp";
			public const string ModelRevision_Description = "ModelRevision_Description";
			public const string ModelRevision_Help = "ModelRevision_Help";
			public const string ModelRevision_Labels = "ModelRevision_Labels";
			public const string ModelRevision_Minor_Version_Number = "ModelRevision_Minor_Version_Number";
			public const string ModelRevision_Notes = "ModelRevision_Notes";
			public const string ModelRevision_Preprocessors = "ModelRevision_Preprocessors";
			public const string ModelRevision_Quality = "ModelRevision_Quality";
			public const string ModelRevision_Quality_Excellent = "ModelRevision_Quality_Excellent";
			public const string ModelRevision_Quality_Good = "ModelRevision_Quality_Good";
			public const string ModelRevision_Quality_Medium = "ModelRevision_Quality_Medium";
			public const string ModelRevision_Quality_Poor = "ModelRevision_Quality_Poor";
			public const string ModelRevision_Quality_Select = "ModelRevision_Quality_Select";
			public const string ModelRevision_Quality_Unknown = "ModelRevision_Quality_Unknown";
			public const string ModelRevision_Settings = "ModelRevision_Settings";
			public const string ModelRevision_Status = "ModelRevision_Status";
			public const string ModelRevision_Status_Alpha = "ModelRevision_Status_Alpha";
			public const string ModelRevision_Status_Beta = "ModelRevision_Status_Beta";
			public const string ModelRevision_Status_Experimental = "ModelRevision_Status_Experimental";
			public const string ModelRevision_Status_New = "ModelRevision_Status_New";
			public const string ModelRevision_Status_Obsolete = "ModelRevision_Status_Obsolete";
			public const string ModelRevision_Status_Production = "ModelRevision_Status_Production";
			public const string ModelRevision_Status_Select = "ModelRevision_Status_Select";
			public const string ModelRevision_Title = "ModelRevision_Title";
			public const string ModelRevision_TrainingSettings = "ModelRevision_TrainingSettings";
			public const string ModelRevision_Version_Number = "ModelRevision_Version_Number";
			public const string ModelSetting_Description = "ModelSetting_Description";
			public const string ModelSetting_Help = "ModelSetting_Help";
			public const string ModelSetting_Title = "ModelSetting_Title";
			public const string ModelSetting_Value = "ModelSetting_Value";
			public const string Preprocessor_ClassName = "Preprocessor_ClassName";
			public const string Preprocessor_Description = "Preprocessor_Description";
			public const string Preprocessor_Help = "Preprocessor_Help";
			public const string Preprocessor_Settings = "Preprocessor_Settings";
			public const string Preprocessor_Title = "Preprocessor_Title";
			public const string PreprocessorSetting_Description = "PreprocessorSetting_Description";
			public const string PreprocessorSetting_Help = "PreprocessorSetting_Help";
			public const string PreprocessorSetting_Title = "PreprocessorSetting_Title";
			public const string PreprocessorSetting_Value = "PreprocessorSetting_Value";
		}
	}
}

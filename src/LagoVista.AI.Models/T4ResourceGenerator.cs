/*12/6/2025 8:01:55 AM*/
using System.Globalization;
using System.Reflection;

//Resources:AIResources:AgentContext_ConversationContext_Description
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
		
		public static string AgentContext_ConversationContext_Description { get { return GetResourceString("AgentContext_ConversationContext_Description"); } }
//Resources:AIResources:AgentContext_ConversationContext_ModelName

		public static string AgentContext_ConversationContext_ModelName { get { return GetResourceString("AgentContext_ConversationContext_ModelName"); } }
//Resources:AIResources:AgentContext_ConversationContext_System

		public static string AgentContext_ConversationContext_System { get { return GetResourceString("AgentContext_ConversationContext_System"); } }
//Resources:AIResources:AgentContext_ConversationContext_System_Help

		public static string AgentContext_ConversationContext_System_Help { get { return GetResourceString("AgentContext_ConversationContext_System_Help"); } }
//Resources:AIResources:AgentContext_ConversationContext_Temperature

		public static string AgentContext_ConversationContext_Temperature { get { return GetResourceString("AgentContext_ConversationContext_Temperature"); } }
//Resources:AIResources:AgentContext_ConversationContext_Temperature_Help

		public static string AgentContext_ConversationContext_Temperature_Help { get { return GetResourceString("AgentContext_ConversationContext_Temperature_Help"); } }
//Resources:AIResources:AgentContext_ConversationContext_Title

		public static string AgentContext_ConversationContext_Title { get { return GetResourceString("AgentContext_ConversationContext_Title"); } }
//Resources:AIResources:AgentContext_ConversationContexts

		public static string AgentContext_ConversationContexts { get { return GetResourceString("AgentContext_ConversationContexts"); } }
//Resources:AIResources:AgentContext_DefaultConversationContext

		public static string AgentContext_DefaultConversationContext { get { return GetResourceString("AgentContext_DefaultConversationContext"); } }
//Resources:AIResources:AgentContext_DefaultConversationContext_Select

		public static string AgentContext_DefaultConversationContext_Select { get { return GetResourceString("AgentContext_DefaultConversationContext_Select"); } }
//Resources:AIResources:AgentContext_LlmProvider

		public static string AgentContext_LlmProvider { get { return GetResourceString("AgentContext_LlmProvider"); } }
//Resources:AIResources:AgentContext_LlmProvider_Select

		public static string AgentContext_LlmProvider_Select { get { return GetResourceString("AgentContext_LlmProvider_Select"); } }
//Resources:AIResources:AgentSession_Description

		public static string AgentSession_Description { get { return GetResourceString("AgentSession_Description"); } }
//Resources:AIResources:AgentSession_Help

		public static string AgentSession_Help { get { return GetResourceString("AgentSession_Help"); } }
//Resources:AIResources:AgentSession_Title

		public static string AgentSession_Title { get { return GetResourceString("AgentSession_Title"); } }
//Resources:AIResources:AgentSessions_Title

		public static string AgentSessions_Title { get { return GetResourceString("AgentSessions_Title"); } }
//Resources:AIResources:AiAgentContext_Description

		public static string AiAgentContext_Description { get { return GetResourceString("AiAgentContext_Description"); } }
//Resources:AIResources:AiAgentContext_Title

		public static string AiAgentContext_Title { get { return GetResourceString("AiAgentContext_Title"); } }
//Resources:AIResources:AiAgentContexts_Title

		public static string AiAgentContexts_Title { get { return GetResourceString("AiAgentContexts_Title"); } }
//Resources:AIResources:AIConversation_Description

		public static string AIConversation_Description { get { return GetResourceString("AIConversation_Description"); } }
//Resources:AIResources:AiConversation_Interaction_Description

		public static string AiConversation_Interaction_Description { get { return GetResourceString("AiConversation_Interaction_Description"); } }
//Resources:AIResources:AiConversation_Interaction_Title

		public static string AiConversation_Interaction_Title { get { return GetResourceString("AiConversation_Interaction_Title"); } }
//Resources:AIResources:AiConversation_Title

		public static string AiConversation_Title { get { return GetResourceString("AiConversation_Title"); } }
//Resources:AIResources:AiConversations_Title

		public static string AiConversations_Title { get { return GetResourceString("AiConversations_Title"); } }
//Resources:AIResources:Common_Category

		public static string Common_Category { get { return GetResourceString("Common_Category"); } }
//Resources:AIResources:Common_Datestamp

		public static string Common_Datestamp { get { return GetResourceString("Common_Datestamp"); } }
//Resources:AIResources:Common_Description

		public static string Common_Description { get { return GetResourceString("Common_Description"); } }
//Resources:AIResources:Common_Icon

		public static string Common_Icon { get { return GetResourceString("Common_Icon"); } }
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
//Resources:AIResources:Common_SelectCategory

		public static string Common_SelectCategory { get { return GetResourceString("Common_SelectCategory"); } }
//Resources:AIResources:Common_Status_Aborted

		public static string Common_Status_Aborted { get { return GetResourceString("Common_Status_Aborted"); } }
//Resources:AIResources:Common_Status_Completed

		public static string Common_Status_Completed { get { return GetResourceString("Common_Status_Completed"); } }
//Resources:AIResources:Common_Status_Failed

		public static string Common_Status_Failed { get { return GetResourceString("Common_Status_Failed"); } }
//Resources:AIResources:Common_Status_New

		public static string Common_Status_New { get { return GetResourceString("Common_Status_New"); } }
//Resources:AIResources:Common_Status_Pending

		public static string Common_Status_Pending { get { return GetResourceString("Common_Status_Pending"); } }
//Resources:AIResources:DDR_Chapter

		public static string DDR_Chapter { get { return GetResourceString("DDR_Chapter"); } }
//Resources:AIResources:DDR_Chapter_Description

		public static string DDR_Chapter_Description { get { return GetResourceString("DDR_Chapter_Description"); } }
//Resources:AIResources:DDR_Chapter_Help

		public static string DDR_Chapter_Help { get { return GetResourceString("DDR_Chapter_Help"); } }
//Resources:AIResources:DDR_Description

		public static string DDR_Description { get { return GetResourceString("DDR_Description"); } }
//Resources:AIResources:DDR_Help

		public static string DDR_Help { get { return GetResourceString("DDR_Help"); } }
//Resources:AIResources:DDR_Title

		public static string DDR_Title { get { return GetResourceString("DDR_Title"); } }
//Resources:AIResources:Ddr_Tla

		public static string Ddr_Tla { get { return GetResourceString("Ddr_Tla"); } }
//Resources:AIResources:Ddr_Tla_Description

		public static string Ddr_Tla_Description { get { return GetResourceString("Ddr_Tla_Description"); } }
//Resources:AIResources:Ddr_Tla_Help

		public static string Ddr_Tla_Help { get { return GetResourceString("Ddr_Tla_Help"); } }
//Resources:AIResources:DDRs_Title

		public static string DDRs_Title { get { return GetResourceString("DDRs_Title"); } }
//Resources:AIResources:DdrTla_Catalog

		public static string DdrTla_Catalog { get { return GetResourceString("DdrTla_Catalog"); } }
//Resources:AIResources:DdrTla_Catalog_Description

		public static string DdrTla_Catalog_Description { get { return GetResourceString("DdrTla_Catalog_Description"); } }
//Resources:AIResources:DdrTla_Catalog_Help

		public static string DdrTla_Catalog_Help { get { return GetResourceString("DdrTla_Catalog_Help"); } }
//Resources:AIResources:Experiemnt_Help

		public static string Experiemnt_Help { get { return GetResourceString("Experiemnt_Help"); } }
//Resources:AIResources:Experiment_Description

		public static string Experiment_Description { get { return GetResourceString("Experiment_Description"); } }
//Resources:AIResources:Experiment_Instructions

		public static string Experiment_Instructions { get { return GetResourceString("Experiment_Instructions"); } }
//Resources:AIResources:Experiment_Title

		public static string Experiment_Title { get { return GetResourceString("Experiment_Title"); } }
//Resources:AIResources:InputType_DataPoints

		public static string InputType_DataPoints { get { return GetResourceString("InputType_DataPoints"); } }
//Resources:AIResources:InputType_Image

		public static string InputType_Image { get { return GetResourceString("InputType_Image"); } }
//Resources:AIResources:Label_Description

		public static string Label_Description { get { return GetResourceString("Label_Description"); } }
//Resources:AIResources:Label_Enabled

		public static string Label_Enabled { get { return GetResourceString("Label_Enabled"); } }
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
//Resources:AIResources:Label_Visible

		public static string Label_Visible { get { return GetResourceString("Label_Visible"); } }
//Resources:AIResources:LabelSet_Help

		public static string LabelSet_Help { get { return GetResourceString("LabelSet_Help"); } }
//Resources:AIResources:LabelSet_Labels

		public static string LabelSet_Labels { get { return GetResourceString("LabelSet_Labels"); } }
//Resources:AIResources:LabelSet_Title

		public static string LabelSet_Title { get { return GetResourceString("LabelSet_Title"); } }
//Resources:AIResources:LabelSets_Title

		public static string LabelSets_Title { get { return GetResourceString("LabelSets_Title"); } }
//Resources:AIResources:LlmProvider_OpenAI

		public static string LlmProvider_OpenAI { get { return GetResourceString("LlmProvider_OpenAI"); } }
//Resources:AIResources:MergeMethod_Merge

		public static string MergeMethod_Merge { get { return GetResourceString("MergeMethod_Merge"); } }
//Resources:AIResources:MergeMethod_Rebase

		public static string MergeMethod_Rebase { get { return GetResourceString("MergeMethod_Rebase"); } }
//Resources:AIResources:MergeMethod_Squash

		public static string MergeMethod_Squash { get { return GetResourceString("MergeMethod_Squash"); } }
//Resources:AIResources:Model_Description

		public static string Model_Description { get { return GetResourceString("Model_Description"); } }
//Resources:AIResources:Model_Experiments

		public static string Model_Experiments { get { return GetResourceString("Model_Experiments"); } }
//Resources:AIResources:Model_Help

		public static string Model_Help { get { return GetResourceString("Model_Help"); } }
//Resources:AIResources:Model_LabelSet

		public static string Model_LabelSet { get { return GetResourceString("Model_LabelSet"); } }
//Resources:AIResources:Model_LabelSet_Help

		public static string Model_LabelSet_Help { get { return GetResourceString("Model_LabelSet_Help"); } }
//Resources:AIResources:Model_ModelCategory

		public static string Model_ModelCategory { get { return GetResourceString("Model_ModelCategory"); } }
//Resources:AIResources:Model_ModelCategory_Select

		public static string Model_ModelCategory_Select { get { return GetResourceString("Model_ModelCategory_Select"); } }
//Resources:AIResources:Model_ModelType

		public static string Model_ModelType { get { return GetResourceString("Model_ModelType"); } }
//Resources:AIResources:Model_PreferredRevision

		public static string Model_PreferredRevision { get { return GetResourceString("Model_PreferredRevision"); } }
//Resources:AIResources:Model_PreferredRevision_Select

		public static string Model_PreferredRevision_Select { get { return GetResourceString("Model_PreferredRevision_Select"); } }
//Resources:AIResources:Model_Revisions

		public static string Model_Revisions { get { return GetResourceString("Model_Revisions"); } }
//Resources:AIResources:Model_Title

		public static string Model_Title { get { return GetResourceString("Model_Title"); } }
//Resources:AIResources:Model_Type

		public static string Model_Type { get { return GetResourceString("Model_Type"); } }
//Resources:AIResources:Model_Type_Onnx

		public static string Model_Type_Onnx { get { return GetResourceString("Model_Type_Onnx"); } }
//Resources:AIResources:Model_Type_PyTorch

		public static string Model_Type_PyTorch { get { return GetResourceString("Model_Type_PyTorch"); } }
//Resources:AIResources:Model_Type_Select

		public static string Model_Type_Select { get { return GetResourceString("Model_Type_Select"); } }
//Resources:AIResources:Model_Type_TensorFlow

		public static string Model_Type_TensorFlow { get { return GetResourceString("Model_Type_TensorFlow"); } }
//Resources:AIResources:Model_Type_TensorFlow_Lite

		public static string Model_Type_TensorFlow_Lite { get { return GetResourceString("Model_Type_TensorFlow_Lite"); } }
//Resources:AIResources:ModelCategories_Title

		public static string ModelCategories_Title { get { return GetResourceString("ModelCategories_Title"); } }
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
//Resources:AIResources:ModelRevision_FileName

		public static string ModelRevision_FileName { get { return GetResourceString("ModelRevision_FileName"); } }
//Resources:AIResources:ModelRevision_Help

		public static string ModelRevision_Help { get { return GetResourceString("ModelRevision_Help"); } }
//Resources:AIResources:ModelRevision_InputShape

		public static string ModelRevision_InputShape { get { return GetResourceString("ModelRevision_InputShape"); } }
//Resources:AIResources:ModelRevision_InputShape_Help

		public static string ModelRevision_InputShape_Help { get { return GetResourceString("ModelRevision_InputShape_Help"); } }
//Resources:AIResources:ModelRevision_InputType

		public static string ModelRevision_InputType { get { return GetResourceString("ModelRevision_InputType"); } }
//Resources:AIResources:ModelRevision_InputType_Select

		public static string ModelRevision_InputType_Select { get { return GetResourceString("ModelRevision_InputType_Select"); } }
//Resources:AIResources:ModelRevision_Labels

		public static string ModelRevision_Labels { get { return GetResourceString("ModelRevision_Labels"); } }
//Resources:AIResources:ModelRevision_LabelSet

		public static string ModelRevision_LabelSet { get { return GetResourceString("ModelRevision_LabelSet"); } }
//Resources:AIResources:ModelRevision_LabelSet_Help

		public static string ModelRevision_LabelSet_Help { get { return GetResourceString("ModelRevision_LabelSet_Help"); } }
//Resources:AIResources:ModelRevision_Minor_Version_Number

		public static string ModelRevision_Minor_Version_Number { get { return GetResourceString("ModelRevision_Minor_Version_Number"); } }
//Resources:AIResources:ModelRevision_ModelFile

		public static string ModelRevision_ModelFile { get { return GetResourceString("ModelRevision_ModelFile"); } }
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
//Resources:AIResources:ModelRevision_TrainingAccuracy

		public static string ModelRevision_TrainingAccuracy { get { return GetResourceString("ModelRevision_TrainingAccuracy"); } }
//Resources:AIResources:ModelRevision_TrainingSettings

		public static string ModelRevision_TrainingSettings { get { return GetResourceString("ModelRevision_TrainingSettings"); } }
//Resources:AIResources:ModelRevision_ValidationAccuracy

		public static string ModelRevision_ValidationAccuracy { get { return GetResourceString("ModelRevision_ValidationAccuracy"); } }
//Resources:AIResources:ModelRevision_Version

		public static string ModelRevision_Version { get { return GetResourceString("ModelRevision_Version"); } }
//Resources:AIResources:ModelRevision_Version_Number

		public static string ModelRevision_Version_Number { get { return GetResourceString("ModelRevision_Version_Number"); } }
//Resources:AIResources:Models_Title

		public static string Models_Title { get { return GetResourceString("Models_Title"); } }
//Resources:AIResources:ModelSetting_Description

		public static string ModelSetting_Description { get { return GetResourceString("ModelSetting_Description"); } }
//Resources:AIResources:ModelSetting_Help

		public static string ModelSetting_Help { get { return GetResourceString("ModelSetting_Help"); } }
//Resources:AIResources:ModelSetting_Title

		public static string ModelSetting_Title { get { return GetResourceString("ModelSetting_Title"); } }
//Resources:AIResources:ModelSetting_Value

		public static string ModelSetting_Value { get { return GetResourceString("ModelSetting_Value"); } }
//Resources:AIResources:OperationKind_Code

		public static string OperationKind_Code { get { return GetResourceString("OperationKind_Code"); } }
//Resources:AIResources:OperationKind_Domain

		public static string OperationKind_Domain { get { return GetResourceString("OperationKind_Domain"); } }
//Resources:AIResources:OperationKind_Image

		public static string OperationKind_Image { get { return GetResourceString("OperationKind_Image"); } }
//Resources:AIResources:OperationKind_Text

		public static string OperationKind_Text { get { return GetResourceString("OperationKind_Text"); } }
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
//Resources:AIResources:SourceOrganization_AppId

		public static string SourceOrganization_AppId { get { return GetResourceString("SourceOrganization_AppId"); } }
//Resources:AIResources:SourceOrganization_AppId_Help

		public static string SourceOrganization_AppId_Help { get { return GetResourceString("SourceOrganization_AppId_Help"); } }
//Resources:AIResources:SourceOrganization_Help

		public static string SourceOrganization_Help { get { return GetResourceString("SourceOrganization_Help"); } }
//Resources:AIResources:SourceOrganization_InstallationId

		public static string SourceOrganization_InstallationId { get { return GetResourceString("SourceOrganization_InstallationId"); } }
//Resources:AIResources:SourceOrganization_InstallationId_Help

		public static string SourceOrganization_InstallationId_Help { get { return GetResourceString("SourceOrganization_InstallationId_Help"); } }
//Resources:AIResources:SourceOrganization_Name

		public static string SourceOrganization_Name { get { return GetResourceString("SourceOrganization_Name"); } }
//Resources:AIResources:SourceOrganization_Name_Help

		public static string SourceOrganization_Name_Help { get { return GetResourceString("SourceOrganization_Name_Help"); } }
//Resources:AIResources:SourceOrganization_PrivateKey

		public static string SourceOrganization_PrivateKey { get { return GetResourceString("SourceOrganization_PrivateKey"); } }
//Resources:AIResources:SourceOrganization_PrivateKey_Help

		public static string SourceOrganization_PrivateKey_Help { get { return GetResourceString("SourceOrganization_PrivateKey_Help"); } }
//Resources:AIResources:SourceOrganization_ProductName

		public static string SourceOrganization_ProductName { get { return GetResourceString("SourceOrganization_ProductName"); } }
//Resources:AIResources:SourceOrganization_ProductName_Help

		public static string SourceOrganization_ProductName_Help { get { return GetResourceString("SourceOrganization_ProductName_Help"); } }
//Resources:AIResources:SourceOrganization_Repositories

		public static string SourceOrganization_Repositories { get { return GetResourceString("SourceOrganization_Repositories"); } }
//Resources:AIResources:SourceOrganization_Title

		public static string SourceOrganization_Title { get { return GetResourceString("SourceOrganization_Title"); } }
//Resources:AIResources:SourceOrganization_WebHookSecret

		public static string SourceOrganization_WebHookSecret { get { return GetResourceString("SourceOrganization_WebHookSecret"); } }
//Resources:AIResources:SourceOrganization_WebHookSecret_Help

		public static string SourceOrganization_WebHookSecret_Help { get { return GetResourceString("SourceOrganization_WebHookSecret_Help"); } }
//Resources:AIResources:SourceOrganizationRepository_DeleteOnMerge

		public static string SourceOrganizationRepository_DeleteOnMerge { get { return GetResourceString("SourceOrganizationRepository_DeleteOnMerge"); } }
//Resources:AIResources:SourceOrganizationRepository_DeleteOnMerge_Help

		public static string SourceOrganizationRepository_DeleteOnMerge_Help { get { return GetResourceString("SourceOrganizationRepository_DeleteOnMerge_Help"); } }
//Resources:AIResources:SourceOrganizationRepository_Help

		public static string SourceOrganizationRepository_Help { get { return GetResourceString("SourceOrganizationRepository_Help"); } }
//Resources:AIResources:SourceOrganizationRepository_MergeMethod

		public static string SourceOrganizationRepository_MergeMethod { get { return GetResourceString("SourceOrganizationRepository_MergeMethod"); } }
//Resources:AIResources:SourceOrganizationRepository_MergeMethod_Select

		public static string SourceOrganizationRepository_MergeMethod_Select { get { return GetResourceString("SourceOrganizationRepository_MergeMethod_Select"); } }
//Resources:AIResources:SourceOrganizationRepository_RepoPath

		public static string SourceOrganizationRepository_RepoPath { get { return GetResourceString("SourceOrganizationRepository_RepoPath"); } }
//Resources:AIResources:SourceOrganizationRepository_RepoPath_Help

		public static string SourceOrganizationRepository_RepoPath_Help { get { return GetResourceString("SourceOrganizationRepository_RepoPath_Help"); } }
//Resources:AIResources:SourceOrganizationRepository_Title

		public static string SourceOrganizationRepository_Title { get { return GetResourceString("SourceOrganizationRepository_Title"); } }
//Resources:AIResources:TrainingDataSet_Description

		public static string TrainingDataSet_Description { get { return GetResourceString("TrainingDataSet_Description"); } }
//Resources:AIResources:TrainingDataSet_Help

		public static string TrainingDataSet_Help { get { return GetResourceString("TrainingDataSet_Help"); } }
//Resources:AIResources:TrainingDataSet_Title

		public static string TrainingDataSet_Title { get { return GetResourceString("TrainingDataSet_Title"); } }
//Resources:AIResources:VectorDatabase_ApiKey

		public static string VectorDatabase_ApiKey { get { return GetResourceString("VectorDatabase_ApiKey"); } }
//Resources:AIResources:VectorDatabase_AzureAccountId

		public static string VectorDatabase_AzureAccountId { get { return GetResourceString("VectorDatabase_AzureAccountId"); } }
//Resources:AIResources:VectorDatabase_AzureAccountId_Help

		public static string VectorDatabase_AzureAccountId_Help { get { return GetResourceString("VectorDatabase_AzureAccountId_Help"); } }
//Resources:AIResources:VectorDatabase_AzureApiToken

		public static string VectorDatabase_AzureApiToken { get { return GetResourceString("VectorDatabase_AzureApiToken"); } }
//Resources:AIResources:VectorDatabase_AzureApiToken_Help

		public static string VectorDatabase_AzureApiToken_Help { get { return GetResourceString("VectorDatabase_AzureApiToken_Help"); } }
//Resources:AIResources:VectorDatabase_AzureBlobContainerName

		public static string VectorDatabase_AzureBlobContainerName { get { return GetResourceString("VectorDatabase_AzureBlobContainerName"); } }
//Resources:AIResources:VectorDatabase_AzureBlobContainerName_Help

		public static string VectorDatabase_AzureBlobContainerName_Help { get { return GetResourceString("VectorDatabase_AzureBlobContainerName_Help"); } }
//Resources:AIResources:VectorDatabase_CollectionName

		public static string VectorDatabase_CollectionName { get { return GetResourceString("VectorDatabase_CollectionName"); } }
//Resources:AIResources:VectorDatabase_Description

		public static string VectorDatabase_Description { get { return GetResourceString("VectorDatabase_Description"); } }
//Resources:AIResources:VectorDatabase_OpenAPI_Token

		public static string VectorDatabase_OpenAPI_Token { get { return GetResourceString("VectorDatabase_OpenAPI_Token"); } }
//Resources:AIResources:VectorDatabase_OpenAPI_Token_Help

		public static string VectorDatabase_OpenAPI_Token_Help { get { return GetResourceString("VectorDatabase_OpenAPI_Token_Help"); } }
//Resources:AIResources:VectorDatabase_Title

		public static string VectorDatabase_Title { get { return GetResourceString("VectorDatabase_Title"); } }
//Resources:AIResources:VectorDatabase_Uri

		public static string VectorDatabase_Uri { get { return GetResourceString("VectorDatabase_Uri"); } }
//Resources:AIResources:VectorDatabases_Title

		public static string VectorDatabases_Title { get { return GetResourceString("VectorDatabases_Title"); } }
//Resources:AIResources:VectorDB_LLMEmbeddingModelName

		public static string VectorDB_LLMEmbeddingModelName { get { return GetResourceString("VectorDB_LLMEmbeddingModelName"); } }

		public static class Names
		{
			public const string AgentContext_ConversationContext_Description = "AgentContext_ConversationContext_Description";
			public const string AgentContext_ConversationContext_ModelName = "AgentContext_ConversationContext_ModelName";
			public const string AgentContext_ConversationContext_System = "AgentContext_ConversationContext_System";
			public const string AgentContext_ConversationContext_System_Help = "AgentContext_ConversationContext_System_Help";
			public const string AgentContext_ConversationContext_Temperature = "AgentContext_ConversationContext_Temperature";
			public const string AgentContext_ConversationContext_Temperature_Help = "AgentContext_ConversationContext_Temperature_Help";
			public const string AgentContext_ConversationContext_Title = "AgentContext_ConversationContext_Title";
			public const string AgentContext_ConversationContexts = "AgentContext_ConversationContexts";
			public const string AgentContext_DefaultConversationContext = "AgentContext_DefaultConversationContext";
			public const string AgentContext_DefaultConversationContext_Select = "AgentContext_DefaultConversationContext_Select";
			public const string AgentContext_LlmProvider = "AgentContext_LlmProvider";
			public const string AgentContext_LlmProvider_Select = "AgentContext_LlmProvider_Select";
			public const string AgentSession_Description = "AgentSession_Description";
			public const string AgentSession_Help = "AgentSession_Help";
			public const string AgentSession_Title = "AgentSession_Title";
			public const string AgentSessions_Title = "AgentSessions_Title";
			public const string AiAgentContext_Description = "AiAgentContext_Description";
			public const string AiAgentContext_Title = "AiAgentContext_Title";
			public const string AiAgentContexts_Title = "AiAgentContexts_Title";
			public const string AIConversation_Description = "AIConversation_Description";
			public const string AiConversation_Interaction_Description = "AiConversation_Interaction_Description";
			public const string AiConversation_Interaction_Title = "AiConversation_Interaction_Title";
			public const string AiConversation_Title = "AiConversation_Title";
			public const string AiConversations_Title = "AiConversations_Title";
			public const string Common_Category = "Common_Category";
			public const string Common_Datestamp = "Common_Datestamp";
			public const string Common_Description = "Common_Description";
			public const string Common_Icon = "Common_Icon";
			public const string Common_Key = "Common_Key";
			public const string Common_Key_Help = "Common_Key_Help";
			public const string Common_Key_Validation = "Common_Key_Validation";
			public const string Common_Name = "Common_Name";
			public const string Common_Notes = "Common_Notes";
			public const string Common_SelectCategory = "Common_SelectCategory";
			public const string Common_Status_Aborted = "Common_Status_Aborted";
			public const string Common_Status_Completed = "Common_Status_Completed";
			public const string Common_Status_Failed = "Common_Status_Failed";
			public const string Common_Status_New = "Common_Status_New";
			public const string Common_Status_Pending = "Common_Status_Pending";
			public const string DDR_Chapter = "DDR_Chapter";
			public const string DDR_Chapter_Description = "DDR_Chapter_Description";
			public const string DDR_Chapter_Help = "DDR_Chapter_Help";
			public const string DDR_Description = "DDR_Description";
			public const string DDR_Help = "DDR_Help";
			public const string DDR_Title = "DDR_Title";
			public const string Ddr_Tla = "Ddr_Tla";
			public const string Ddr_Tla_Description = "Ddr_Tla_Description";
			public const string Ddr_Tla_Help = "Ddr_Tla_Help";
			public const string DDRs_Title = "DDRs_Title";
			public const string DdrTla_Catalog = "DdrTla_Catalog";
			public const string DdrTla_Catalog_Description = "DdrTla_Catalog_Description";
			public const string DdrTla_Catalog_Help = "DdrTla_Catalog_Help";
			public const string Experiemnt_Help = "Experiemnt_Help";
			public const string Experiment_Description = "Experiment_Description";
			public const string Experiment_Instructions = "Experiment_Instructions";
			public const string Experiment_Title = "Experiment_Title";
			public const string InputType_DataPoints = "InputType_DataPoints";
			public const string InputType_Image = "InputType_Image";
			public const string Label_Description = "Label_Description";
			public const string Label_Enabled = "Label_Enabled";
			public const string Label_Help = "Label_Help";
			public const string Label_Icon = "Label_Icon";
			public const string Label_Index = "Label_Index";
			public const string Label_Key = "Label_Key";
			public const string Label_Title = "Label_Title";
			public const string Label_Visible = "Label_Visible";
			public const string LabelSet_Help = "LabelSet_Help";
			public const string LabelSet_Labels = "LabelSet_Labels";
			public const string LabelSet_Title = "LabelSet_Title";
			public const string LabelSets_Title = "LabelSets_Title";
			public const string LlmProvider_OpenAI = "LlmProvider_OpenAI";
			public const string MergeMethod_Merge = "MergeMethod_Merge";
			public const string MergeMethod_Rebase = "MergeMethod_Rebase";
			public const string MergeMethod_Squash = "MergeMethod_Squash";
			public const string Model_Description = "Model_Description";
			public const string Model_Experiments = "Model_Experiments";
			public const string Model_Help = "Model_Help";
			public const string Model_LabelSet = "Model_LabelSet";
			public const string Model_LabelSet_Help = "Model_LabelSet_Help";
			public const string Model_ModelCategory = "Model_ModelCategory";
			public const string Model_ModelCategory_Select = "Model_ModelCategory_Select";
			public const string Model_ModelType = "Model_ModelType";
			public const string Model_PreferredRevision = "Model_PreferredRevision";
			public const string Model_PreferredRevision_Select = "Model_PreferredRevision_Select";
			public const string Model_Revisions = "Model_Revisions";
			public const string Model_Title = "Model_Title";
			public const string Model_Type = "Model_Type";
			public const string Model_Type_Onnx = "Model_Type_Onnx";
			public const string Model_Type_PyTorch = "Model_Type_PyTorch";
			public const string Model_Type_Select = "Model_Type_Select";
			public const string Model_Type_TensorFlow = "Model_Type_TensorFlow";
			public const string Model_Type_TensorFlow_Lite = "Model_Type_TensorFlow_Lite";
			public const string ModelCategories_Title = "ModelCategories_Title";
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
			public const string ModelRevision_FileName = "ModelRevision_FileName";
			public const string ModelRevision_Help = "ModelRevision_Help";
			public const string ModelRevision_InputShape = "ModelRevision_InputShape";
			public const string ModelRevision_InputShape_Help = "ModelRevision_InputShape_Help";
			public const string ModelRevision_InputType = "ModelRevision_InputType";
			public const string ModelRevision_InputType_Select = "ModelRevision_InputType_Select";
			public const string ModelRevision_Labels = "ModelRevision_Labels";
			public const string ModelRevision_LabelSet = "ModelRevision_LabelSet";
			public const string ModelRevision_LabelSet_Help = "ModelRevision_LabelSet_Help";
			public const string ModelRevision_Minor_Version_Number = "ModelRevision_Minor_Version_Number";
			public const string ModelRevision_ModelFile = "ModelRevision_ModelFile";
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
			public const string ModelRevision_TrainingAccuracy = "ModelRevision_TrainingAccuracy";
			public const string ModelRevision_TrainingSettings = "ModelRevision_TrainingSettings";
			public const string ModelRevision_ValidationAccuracy = "ModelRevision_ValidationAccuracy";
			public const string ModelRevision_Version = "ModelRevision_Version";
			public const string ModelRevision_Version_Number = "ModelRevision_Version_Number";
			public const string Models_Title = "Models_Title";
			public const string ModelSetting_Description = "ModelSetting_Description";
			public const string ModelSetting_Help = "ModelSetting_Help";
			public const string ModelSetting_Title = "ModelSetting_Title";
			public const string ModelSetting_Value = "ModelSetting_Value";
			public const string OperationKind_Code = "OperationKind_Code";
			public const string OperationKind_Domain = "OperationKind_Domain";
			public const string OperationKind_Image = "OperationKind_Image";
			public const string OperationKind_Text = "OperationKind_Text";
			public const string Preprocessor_ClassName = "Preprocessor_ClassName";
			public const string Preprocessor_Description = "Preprocessor_Description";
			public const string Preprocessor_Help = "Preprocessor_Help";
			public const string Preprocessor_Settings = "Preprocessor_Settings";
			public const string Preprocessor_Title = "Preprocessor_Title";
			public const string PreprocessorSetting_Description = "PreprocessorSetting_Description";
			public const string PreprocessorSetting_Help = "PreprocessorSetting_Help";
			public const string PreprocessorSetting_Title = "PreprocessorSetting_Title";
			public const string PreprocessorSetting_Value = "PreprocessorSetting_Value";
			public const string SourceOrganization_AppId = "SourceOrganization_AppId";
			public const string SourceOrganization_AppId_Help = "SourceOrganization_AppId_Help";
			public const string SourceOrganization_Help = "SourceOrganization_Help";
			public const string SourceOrganization_InstallationId = "SourceOrganization_InstallationId";
			public const string SourceOrganization_InstallationId_Help = "SourceOrganization_InstallationId_Help";
			public const string SourceOrganization_Name = "SourceOrganization_Name";
			public const string SourceOrganization_Name_Help = "SourceOrganization_Name_Help";
			public const string SourceOrganization_PrivateKey = "SourceOrganization_PrivateKey";
			public const string SourceOrganization_PrivateKey_Help = "SourceOrganization_PrivateKey_Help";
			public const string SourceOrganization_ProductName = "SourceOrganization_ProductName";
			public const string SourceOrganization_ProductName_Help = "SourceOrganization_ProductName_Help";
			public const string SourceOrganization_Repositories = "SourceOrganization_Repositories";
			public const string SourceOrganization_Title = "SourceOrganization_Title";
			public const string SourceOrganization_WebHookSecret = "SourceOrganization_WebHookSecret";
			public const string SourceOrganization_WebHookSecret_Help = "SourceOrganization_WebHookSecret_Help";
			public const string SourceOrganizationRepository_DeleteOnMerge = "SourceOrganizationRepository_DeleteOnMerge";
			public const string SourceOrganizationRepository_DeleteOnMerge_Help = "SourceOrganizationRepository_DeleteOnMerge_Help";
			public const string SourceOrganizationRepository_Help = "SourceOrganizationRepository_Help";
			public const string SourceOrganizationRepository_MergeMethod = "SourceOrganizationRepository_MergeMethod";
			public const string SourceOrganizationRepository_MergeMethod_Select = "SourceOrganizationRepository_MergeMethod_Select";
			public const string SourceOrganizationRepository_RepoPath = "SourceOrganizationRepository_RepoPath";
			public const string SourceOrganizationRepository_RepoPath_Help = "SourceOrganizationRepository_RepoPath_Help";
			public const string SourceOrganizationRepository_Title = "SourceOrganizationRepository_Title";
			public const string TrainingDataSet_Description = "TrainingDataSet_Description";
			public const string TrainingDataSet_Help = "TrainingDataSet_Help";
			public const string TrainingDataSet_Title = "TrainingDataSet_Title";
			public const string VectorDatabase_ApiKey = "VectorDatabase_ApiKey";
			public const string VectorDatabase_AzureAccountId = "VectorDatabase_AzureAccountId";
			public const string VectorDatabase_AzureAccountId_Help = "VectorDatabase_AzureAccountId_Help";
			public const string VectorDatabase_AzureApiToken = "VectorDatabase_AzureApiToken";
			public const string VectorDatabase_AzureApiToken_Help = "VectorDatabase_AzureApiToken_Help";
			public const string VectorDatabase_AzureBlobContainerName = "VectorDatabase_AzureBlobContainerName";
			public const string VectorDatabase_AzureBlobContainerName_Help = "VectorDatabase_AzureBlobContainerName_Help";
			public const string VectorDatabase_CollectionName = "VectorDatabase_CollectionName";
			public const string VectorDatabase_Description = "VectorDatabase_Description";
			public const string VectorDatabase_OpenAPI_Token = "VectorDatabase_OpenAPI_Token";
			public const string VectorDatabase_OpenAPI_Token_Help = "VectorDatabase_OpenAPI_Token_Help";
			public const string VectorDatabase_Title = "VectorDatabase_Title";
			public const string VectorDatabase_Uri = "VectorDatabase_Uri";
			public const string VectorDatabases_Title = "VectorDatabases_Title";
			public const string VectorDB_LLMEmbeddingModelName = "VectorDB_LLMEmbeddingModelName";
		}
	}
}


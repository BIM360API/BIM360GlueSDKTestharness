// Copyright 2012 Autodesk, Inc.  All rights reserved.
// Use of this software is subject to the terms of the Autodesk license agreement 
// provided at the time of installation or download, or which otherwise accompanies 
// this software in either electronic or hard copy form.   

using System;
using System.Collections.Generic;

namespace BIM360SDKTestClient
{
  //==================================================
  // LOGIN
  //==================================================
  [Serializable]
  public class security_login_response_v1
  {
    public string auth_token { get; set; }
    public string user_id { get; set; }
  }

  [Serializable]
  public class system_apikey_response_v1
  {
    public string developer_id { get; set; }
    public string creation_ts { get; set; }
    public int disabled { get; set; }
    public string company_id { get; set; }
    public string company_name { get; set; }
    public string address { get; set; }
    public string description { get; set; }
    public string url { get; set; }
    public string contact_email { get; set; }
    public string contact_phone { get; set; }
    public string api_key { get; set; }
    public int service_provider_access { get; set; }
    public string api_lock_ip_addresses { get; set; }
  }


  [Serializable]
  public class user_info_response_v1
  {
    public string user_id { get; set; }
    public string email { get; set; }
    public string login_name { get; set; }
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string company { get; set; }
    public string created_date { get; set; }
    public int num_sessions { get; set; }
    public int disabled { get; set; }
    public int password_change_required { get; set; }
  }

  [Serializable]
  public class user_company_roster_response_v1
  {
    public int page { get; set; }
    public int page_size { get; set; }
    public int total_result_size { get; set; }
    public int more_pages { get; set; }

    public user_info_response_v1[] user_roster;
  }

  [Serializable]
  public class project_list_response_v1
  {
    // General Description of result set
    public int page { get; set; }
    public int page_size { get; set; }
    public int total_result_size { get; set; }
    public int more_pages { get; set; }

    // The user list for the project
    public project_info_response_v1[] project_list;
  }

  public class glue_folder_node
  {
    // This is the Model Information
    public string type { get; set; }  // MODEL, FOLDER
    public string name { get; set; }
    public string object_id { get; set; }
    public string action_id { get; set; }
    public string version_id { get; set; }
    public int version { get; set; }
    public string created_by { get; set; }
    public string created_date { get; set; }
    public string parent_folder_id { get; set; }
    public int is_merged_model { get; set; }
    // Support merged model streaming
    public int merged_model_available { get; set; }
    public string merged_model_parsing_status { get; set; }

    public glue_folder_node[] folder_contents { get; set; }
    public glue_folder_node[] merged_submodels { get; set; }
  }


  [Serializable]
  public class project_info_response_v1
  {
    // This is the Project Information
    public string project_id { get; set; }
    public string project_name { get; set; }
    public string company_id { get; set; }
    public string created_date { get; set; }
    public string modify_date { get; set; }

    // The Project folder tree
    public glue_folder_node[] folder_tree;

    // The user list for the project
    public user_info_response_v1[] project_roster;
  }

  [Serializable]
  public class action_info
  {
    // This is the Model Information
    public string action_id { get; set; }
    public string project_id { get; set; }
    public string model_id { get; set; }
    public string model_name { get; set; }
    public string subject { get; set; }
    public string type { get; set; }
    public string type_object_id { get; set; }
    // public string value { get; set; }
    public string created_by { get; set; }
    public string created_date { get; set; }
  }

  [Serializable]
  public class action_search_response_v1
  {
    // General Description of result set
    public int page { get; set; }
    public int page_size { get; set; }
    public int total_result_size { get; set; }
    public int more_pages { get; set; }

    public action_info[] action_list;
  }

  [Serializable]
  public class model_markup
  {
    // The View Node Information
    public string markup_id { get; set; }
    public string action_id { get; set; }
    public string view_id { get; set; }
    public string name { get; set; }

    // Project/Model Info
    public string project_id { get; set; }
    public string model_id { get; set; }
    public string model_version_id { get; set; }
    public int model_version { get; set; }

    public string created_by { get; set; }
    public string created_date { get; set; }
    public string modified_by { get; set; }
    public string modified_date { get; set; }

    public string group_id { get; set; }
  }

  [Serializable]
  public class model_view_node
  {
    // The View Node Information
    public string type { get; set; }  // VIEW, FOLDER
    public string name { get; set; }
    public string object_id { get; set; }
    public string action_id { get; set; }

    // Project/Model Info
    public string project_id { get; set; }
    public string model_id { get; set; }
    public string model_version_id { get; set; }
    public int model_version { get; set; }

    public string created_by { get; set; }
    public string created_date { get; set; }
    public string modified_by { get; set; }
    public string modified_date { get; set; }

    public string global { get; set; }
    public string parent_folder_id { get; set; }

    // For markups associated to this view - only valid for VIEW's that are not folders
    public model_markup[] markups { get; set; }

    // For sub folders
    public model_view_node[] folder_contents { get; set; }

    // View Info for VIEW nodes
    public model_view_info view_info;
  }

  [Serializable]
  public class model_view_info
  {
    // Camera Position
    public string camera_pos_x { get; set; }
    public string camera_pos_y { get; set; }
    public string camera_pos_z { get; set; }

    // Target Position
    public string target_pos_x { get; set; }
    public string target_pos_y { get; set; }
    public string target_pos_z { get; set; }

    // Up Vector
    public string up_vector_x { get; set; }
    public string up_vector_y { get; set; }
    public string up_vector_z { get; set; }

    // Field of View Dimensions
    public string fov_width { get; set; }
    public string fov_height { get; set; }

    // Camera View Mode and Navigation Mode
    public string camera_view_mode { get; set; } // perspective / orthographic
    public string navigation_mode { get; set; }  // relativeorbit, pan, walk, look
  }

  [Serializable]
  public class model_clash_report
  {
    public string clash_report_id { get; set; }
    public string action_id { get; set; }
    public string name { get; set; }

    public string project_id { get; set; }
    public string model_id { get; set; }
    public string model_version_id { get; set; }
    public int model_version { get; set; }
    public string model_name { get; set; }

    public string created_by { get; set; }
    public string created_date { get; set; }
    public string modified_by { get; set; }
    public string modified_date { get; set; }
    public string comments { get; set; }
  }

  [Serializable]
  public class model_history
  {
    public int model_version { get; set; }
    public string model_id { get; set; }
    public string action_id { get; set; }
    public string model_version_id { get; set; }
    public string created_by { get; set; }
    public string created_date { get; set; }
    public string modified_by { get; set; }
  }

  [Serializable]
  public class model_info_response_v1
  {
    // This is the mo_file information for the BIM 360 Glue Model
    public string action_id { get; set; }
    public string company_id { get; set; }
    public string project_id { get; set; }
    public string model_id { get; set; }
    public int model_version { get; set; }
    public string model_version_id { get; set; }
    public string model_name { get; set; }
    public string created_by { get; set; }
    public string created_date { get; set; }
    public string modified_by { get; set; }
    public string modified_date { get; set; }
    public string parent_folder_id { get; set; }
    // public int is_folder { get; set; }
    public int file_parsed_status { get; set; }

    // Support for merged model streaming
    public int merged_model_available { get; set; }
    public string merged_model_parsing_status { get; set; }

    // Other General Info
    public int is_merged_model { get; set; }
    public glue_folder_node[] merged_submodels { get; set; }

    // Version Information
    public model_history[] version_history;

    // Clash reports for the project
    public model_clash_report[] clash_reports { get; set; }

    // Views/Markups for the model
    public model_view_node[] view_tree { get; set; }
  }

  [Serializable]
  public class project_clash_reports_response_v1
  {
    public string project_id { get; set; }
    public string project_name { get; set; }
    public string company_id { get; set; }
    public int report_count { get; set; }
    public model_clash_report[] clash_reports { get; set; }
  }

  [Serializable]
  public class model_markups_response_v1
  {
    // This is the mo_file information for the BIM 360 Glue Model
    public string model_id { get; set; }
    public string model_name { get; set; }
    public string action_id { get; set; }

    // Markups for the model
    public model_markup[] markups { get; set; }
  }

  [Serializable]
  public class company_info
  {
    // This is the individual company information
    public string company_id { get; set; }
  }

  [Serializable]
  public class user_company_list
  {
    // Information for company list
    public int company_count { get; set; }
    public company_info[] list;
  }

  [Serializable]
  public class model_view_tree_info
  {
    // Basic model identifiers
    public string model_id { get; set; }
    public int model_version { get; set; }
    public string model_version_id { get; set; }
    public string model_name { get; set; }

    // Views/Markups for the model
    public model_view_node[] view_tree { get; set; }
  }

  [Serializable]
  public class model_view_tree_response_v1
  {
    // Basic model identifiers
    public string model_id { get; set; }
    public int model_version { get; set; }
    public string model_version_id { get; set; }
    public string model_name { get; set; }

    // Views/Markups for the model
    public model_view_node[] view_tree { get; set; }

    // Is this a merged model... if so, do the sub model info
    public int is_merged_model { get; set; }
    public model_view_tree_info[] merged_submodels { get; set; }
  }

  [Serializable]
  public class model_equipment_set
  {
      // General Information
      public string name { get; set; }
      public string id { get; set; }
      public string update_time { get; set; }
      public string source { get; set; }
  }

  [Serializable]
  public class model_equipment_set_response
  {
      // General Information
      public List<model_equipment_set> equipment_sets { get; set; }
      public int count { get; set; }
  }

}

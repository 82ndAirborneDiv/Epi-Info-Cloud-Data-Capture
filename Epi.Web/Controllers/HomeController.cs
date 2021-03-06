﻿using System;
using System.Web.Mvc;
using Epi.Web.MVC.Models;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Linq;
using Epi.Core.EnterInterpreter;
using System.Collections.Generic;
using System.Web.Security;
using System.Configuration;
using System.Web;
using System.Web.Caching;
using System.Web.UI;
using System.Reflection;
using System.Diagnostics;
using Epi.Web.Enter.Common.Message;
using Epi.Web.MVC.Utility;
using Epi.Web.Enter.Common.DTO;
using System.Web.Configuration;
using System.Text;
namespace Epi.Web.MVC.Controllers
{
    [Authorize]
	[AllowCrossSite]
	public class HomeController : Controller
    {
        private Epi.Web.MVC.Facade.ISurveyFacade _isurveyFacade;
        private IEnumerable<XElement> PageFields;
        private string RequiredList = "";
        private int NumberOfPages = -1;
        private int PageSize = -1;
        private int NumberOfResponses = -1;
        List<KeyValuePair<int, string>> Columns = new List<KeyValuePair<int, string>>();

        /// <summary>
        /// injecting surveyFacade to the constructor 
        /// </summary>
        /// <param name="surveyFacade"></param>
        public HomeController(Epi.Web.MVC.Facade.ISurveyFacade isurveyFacade)
        {
            _isurveyFacade = isurveyFacade;
        }

        public ActionResult Default()
        {
            return View("Default");
        }

        [HttpGet]
        public ActionResult Index(string surveyid, int orgid=-1)
        {

            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            int OrgnizationId;
            Session["EditForm"] = null;

            Guid UserId1 = new Guid();
            try
            {
                string SurveyMode = "";
                //SurveyInfoModel surveyInfoModel = GetSurveyInfo(surveyid);
                //  List<FormInfoModel> listOfformInfoModel = GetFormsInfoList(UserId1);

                FormModel FormModel;
               
                GetFormModel(surveyid, UserId, UserId1, out OrgnizationId, out FormModel);

                if (orgid == -1)
                {
                Session["SelectedOrgId"] = OrgnizationId;
                }else{

                    Session["SelectedOrgId"] = orgid;
                }
                System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"(\r\n|\r|\n)+");


                
                bool IsMobileDevice = false;
                IsMobileDevice = this.Request.Browser.IsMobileDevice;
                if (IsMobileDevice) // Because mobile doesn't need RootFormId until button click. 
                {
                    Session["RootFormId"] = null;
                    Session["PageNumber"] = null;
                    Session["SortOrder"] = null;
                    Session["SortField"] = null;
                    Session["SearchCriteria"] = null;
                    Session["SearchModel"] = null;
                }
                Omniture OmnitureObj = Epi.Web.MVC.Utility.OmnitureHelper.GetSettings(SurveyMode, IsMobileDevice);

                ViewBag.Omniture = OmnitureObj;

                string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                ViewBag.Version = version;

                return View(Epi.Web.MVC.Constants.Constant.INDEX_PAGE, FormModel);
            }
            catch (Exception ex)
            {
                Epi.Web.Utility.ExceptionMessage.SendLogMessage(ex, this.HttpContext);
                ExceptionModel ExModel = new ExceptionModel();
                ExModel.ExceptionDetail = ex.StackTrace;
                ExModel.Message = ex.Message;
                return View(Epi.Web.MVC.Constants.Constant.EXCEPTION_PAGE, ExModel);
            }
        }

        private void GetFormModel(string surveyid, int UserId, Guid UserId1, out int OrgnizationId, out FormModel FormModel)
        {
            FormModel = new Models.FormModel();
            FormModel.UserHighestRole = int.Parse(Session["UserHighestRole"].ToString());
            // Get OrganizationList
            OrganizationRequest Request = new OrganizationRequest();
            Request.UserId = UserId;
            Request.UserRole = FormModel.UserHighestRole;
            OrganizationResponse Organizations = _isurveyFacade.GetOrganizationsByUserId(Request);

            FormModel.OrganizationList = Mapper.ToOrganizationModelList(Organizations.OrganizationList);
            //Get Forms
            OrgnizationId = Organizations.OrganizationList[0].OrganizationId;
            FormModel.FormList = GetFormsInfoList(UserId1, OrgnizationId);
            // Set user Info

            FormModel.UserFirstName = Session["UserFirstName"].ToString();
            FormModel.UserLastName = Session["UserLastName"].ToString();
            FormModel.SelectedForm = surveyid;
             
        }

        /// <summary>
        /// redirecting to Survey controller to action method Index
        /// </summary>
        /// <param name="surveyModel"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Index(string surveyid, string AddNewFormId, string EditForm )
        {
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            int CuurentOrgId = int.Parse(Session["SelectedOrgId"].ToString());
            Guid ResponseID = Guid.NewGuid();
            Session["FormValuesHasChanged"] = "";
           
            TempData[Epi.Web.MVC.Constants.Constant.RESPONSE_ID] = Session["RootResponseId"] = ResponseID.ToString();
           

            if(string.IsNullOrEmpty(EditForm) && Session["EditForm"]!=null){
                EditForm = Session["EditForm"].ToString();
            }

            if (!string.IsNullOrEmpty(EditForm) && string.IsNullOrEmpty(AddNewFormId))
            {
                
                Session["RootResponseId"] = EditForm.ToLower();

                Session["IsEditMode"] = true;
                Epi.Web.Enter.Common.DTO.SurveyAnswerDTO surveyAnswerDTO = GetSurveyAnswer(EditForm, Session["RootFormId"].ToString());

               
                Session["RequestedViewId"] = surveyAnswerDTO.ViewId;
                if (Session["RecoverLastRecordVersion"] != null)
                {
                surveyAnswerDTO.RecoverLastRecordVersion = bool.Parse(Session["RecoverLastRecordVersion"].ToString());
                }
                string ChildRecordId = GetChildRecordId(surveyAnswerDTO);
                Session["RecoverLastRecordVersion"] = false;
                return RedirectToAction(Epi.Web.MVC.Constants.Constant.INDEX, Epi.Web.MVC.Constants.Constant.SURVEY_CONTROLLER, new { responseid = ChildRecordId, PageNumber = 1, Edit = "Edit" });
            }
            else
            {
                Session["IsEditMode"] = false;
            }
            bool IsMobileDevice = this.Request.Browser.IsMobileDevice;


            if (IsMobileDevice == false)
            {
                IsMobileDevice = Epi.Web.MVC.Utility.SurveyHelper.IsMobileDevice(this.Request.UserAgent.ToString());
            }

            FormsAuthentication.SetAuthCookie("BeginSurvey", false);
            Session["RootFormId"] = AddNewFormId;
            //create the responseid
            Epi.Web.Enter.Common.DTO.SurveyAnswerDTO SurveyAnswer = _isurveyFacade.CreateSurveyAnswer(AddNewFormId, ResponseID.ToString(), UserId, false, "", false, CuurentOrgId);
            MvcDynamicForms.Form form = _isurveyFacade.GetSurveyFormData(SurveyAnswer.SurveyId, 1, SurveyAnswer, IsMobileDevice,null,null,false,false);
            TempData["Width"] = form.Width + 100;
            SurveyInfoModel surveyInfoModel = Mapper.ToFormInfoModel(form.SurveyInfo);
            // set the survey answer to be production or test 
            SurveyAnswer.IsDraftMode = form.SurveyInfo.IsDraftMode;
            XDocument xdoc = XDocument.Parse(form.SurveyInfo.XML);
              
           ///////////////////////////// Execute - Record Before - start//////////////////////
            Dictionary<string, string> ContextDetailList = new Dictionary<string, string>();
            EnterRule FunctionObject_B = (EnterRule)form.FormCheckCodeObj.GetCommand("level=record&event=before&identifier=");
            SurveyResponseXML SurveyResponseXML = new SurveyResponseXML(PageFields, RequiredList);
            if (FunctionObject_B != null && !FunctionObject_B.IsNull())
            {
                try
                {
                    SurveyAnswer.XML = SurveyResponseXML.CreateResponseDocument(xdoc, SurveyAnswer.XML);
                    Session["RequiredList"] = SurveyResponseXML._RequiredList;
                    //SurveyAnswer.XML = Epi.Web.MVC.Utility.SurveyHelper.CreateResponseDocument(xdoc, SurveyAnswer.XML, RequiredList);
                    this.RequiredList = SurveyResponseXML._RequiredList;
                    form.RequiredFieldsList = this.RequiredList;
                    FunctionObject_B.Context.HiddenFieldList = form.HiddenFieldsList;
                    FunctionObject_B.Context.HighlightedFieldList = form.HighlightedFieldsList;
                    FunctionObject_B.Context.DisabledFieldList = form.DisabledFieldsList;
                    FunctionObject_B.Context.RequiredFieldList = form.RequiredFieldsList;

                    FunctionObject_B.Execute();

                    // field list
                    form.HiddenFieldsList = FunctionObject_B.Context.HiddenFieldList;
                    form.HighlightedFieldsList = FunctionObject_B.Context.HighlightedFieldList;
                    form.DisabledFieldsList = FunctionObject_B.Context.DisabledFieldList;
                    form.RequiredFieldsList = FunctionObject_B.Context.RequiredFieldList;


                    ContextDetailList = Epi.Web.MVC.Utility.SurveyHelper.GetContextDetailList(FunctionObject_B);
                    form = Epi.Web.MVC.Utility.SurveyHelper.UpdateControlsValuesFromContext(form, ContextDetailList);

                    _isurveyFacade.UpdateSurveyResponse(surveyInfoModel, ResponseID.ToString(), form, SurveyAnswer, false, false, 0, SurveyHelper.GetDecryptUserId(Session["UserId"].ToString()));
                }
                catch (Exception ex)
                {
                    // do nothing so that processing
                    // can continue
                }
            }
            else
            {
                SurveyAnswer.XML = SurveyResponseXML.CreateResponseDocument(xdoc, SurveyAnswer.XML);//, RequiredList);
                this.RequiredList = SurveyResponseXML._RequiredList;
                Session["RequiredList"] = SurveyResponseXML._RequiredList;
                form.RequiredFieldsList = RequiredList;
                _isurveyFacade.UpdateSurveyResponse(surveyInfoModel, SurveyAnswer.ResponseId, form, SurveyAnswer, false, false, 0, SurveyHelper.GetDecryptUserId(Session["UserId"].ToString()));
            }

           
            return RedirectToAction(Epi.Web.MVC.Constants.Constant.INDEX, Epi.Web.MVC.Constants.Constant.SURVEY_CONTROLLER, new { responseid = ResponseID, PageNumber = 1, surveyid = surveyInfoModel.SurveyId });
             
        }

        private string GetChildRecordId(SurveyAnswerDTO surveyAnswerDTO)
        {
            SurveyAnswerRequest SurveyAnswerRequest = new SurveyAnswerRequest();
            SurveyAnswerResponse SurveyAnswerResponse = new SurveyAnswerResponse();
            string ChildId = Guid.NewGuid().ToString();
            surveyAnswerDTO.ParentRecordId = surveyAnswerDTO.ResponseId;
            surveyAnswerDTO.ResponseId = ChildId;
            surveyAnswerDTO.Status = 1;
            SurveyAnswerRequest.SurveyAnswerList.Add(surveyAnswerDTO);
            string result;

            //responseId = TempData[Epi.Web.MVC.Constants.Constant.RESPONSE_ID].ToString();
            string Id = Session["UserId"].ToString();
            SurveyAnswerRequest.Criteria.UserId = SurveyHelper.GetDecryptUserId(Id);//_UserId;
            SurveyAnswerRequest.RequestId = ChildId;
            SurveyAnswerRequest.Action = "CreateMulti";
            SurveyAnswerResponse = _isurveyFacade.SetChildRecord(SurveyAnswerRequest);
            result = SurveyAnswerResponse.SurveyResponseList[0].ResponseId.ToString();
            return result;
        }
        //[HttpPost]
        //public ActionResult Index(List<FormInfoModel> model) {
        //    return View("ListResponses", model);
        //}

        [HttpGet]
        [Authorize]
        public ActionResult ReadResponseInfo(string formid, int page = 1)//List<FormInfoModel> ModelList, string formid)
        {
            bool IsMobileDevice = this.Request.Browser.IsMobileDevice;

            var model = new FormResponseInfoModel();

            Session["RootFormId"] = formid;
            model = GetFormResponseInfoModel(formid, page);

            if (IsMobileDevice == false)
            {
                return PartialView("ListResponses", model);
            }
            else
            {
                return View("ListResponses", model);
            }
        }

        [HttpGet]
        [Authorize]
        public ActionResult ReadSortedResponseInfo(string formid, int page, string sort, string sortfield, int orgid,bool reset = false)//List<FormInfoModel> ModelList, string formid)
        {
            //Code added to retain Search Starts
            if(reset)
            {
            Session["SortOrder"] = "";
            Session["SortField"] = "";
            }
            Session["SelectedOrgId"] = orgid;
            if (Session["RootFormId"] != null && Session["RootFormId"].ToString() == formid)
            {
                if (Session["SortOrder"] != null &&
                    !string.IsNullOrEmpty(Session["SortOrder"].ToString()) &&
                    string.IsNullOrEmpty(sort))
                {
                    sort = Session["SortOrder"].ToString();
                }

                if (Session["SortField"] != null &&
                    !string.IsNullOrEmpty(Session["SortField"].ToString()) &&
                    string.IsNullOrEmpty(sortfield))
                {
                    sortfield = Session["SortField"].ToString();
                }

                //if (Session["PageNumber"] != null &&
                //    !string.IsNullOrEmpty(Session["PageNumber"].ToString()) )
                //{
                //    page = Convert.ToInt16(Session["PageNumber"].ToString());
                //}

                Session["SortOrder"] = sort;
                Session["SortField"] = sortfield;
                //Session["PageNumber"] = page;
            }
            else
            {
                Session.Remove("SortOrder");
                Session.Remove("SortField");
                Session.Remove("PageNumber");
            }
            //Code added to retain Search Ends. 

            Session["RootFormId"] = formid;
            Session["PageNumber"] = page;
            bool IsMobileDevice = this.Request.Browser.IsMobileDevice;

            var model = new FormResponseInfoModel();
            model = GetFormResponseInfoModel(formid, page, sort, sortfield, orgid);

            if (IsMobileDevice == false)
            {
                return PartialView("ListResponses", model);
            }
            else
            {
                return View("ListResponses", model);
            }
        }
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult ResetSort(string formid)
        {
            Session["SortOrder"] = null;
            Session["SortField"] = null;
            return Json(true);
        }
        private string CreateSearchCriteria(System.Collections.Specialized.NameValueCollection nameValueCollection, SearchBoxModel SearchModel, FormResponseInfoModel Model)
        {
            FormCollection Collection = new FormCollection(nameValueCollection);



            StringBuilder searchBuilder = new StringBuilder();

            if (ValidateSearchFields(Collection))
            {

                if (Collection["col1"].Length > 0 && Collection["val1"].Length > 0)
                {
                    searchBuilder.Append(Collection["col1"] + "='" + Collection["val1"] + "'");
                    SearchModel.SearchCol1 = Collection["col1"];
                    SearchModel.Value1 = Collection["val1"];
                }
                if (Collection["col2"].Length > 0 && Collection["val2"].Length > 0)
                {
                    searchBuilder.Append(" AND " + Collection["col2"] + "='" + Collection["val2"] + "'");
                    SearchModel.SearchCol2 = Collection["col2"];
                    SearchModel.Value2 = Collection["val2"];
                }
                if (Collection["col3"].Length > 0 && Collection["val3"].Length > 0)
                {
                    searchBuilder.Append(" AND " + Collection["col3"] + "='" + Collection["val3"] + "'");
                    SearchModel.SearchCol3 = Collection["col3"];
                    SearchModel.Value3 = Collection["val3"];
                }
                if (Collection["col4"].Length > 0 && Collection["val4"].Length > 0)
                {
                    searchBuilder.Append(" AND " + Collection["col4"] + "='" + Collection["val4"] + "'");
                    SearchModel.SearchCol4 = Collection["col4"];
                    SearchModel.Value4 = Collection["val4"];
                }
                if (Collection["col5"].Length > 0 && Collection["val5"].Length > 0)
                {
                    searchBuilder.Append(" AND " + Collection["col5"] + "='" + Collection["val5"] + "'");
                    SearchModel.SearchCol5 = Collection["col5"];
                    SearchModel.Value5 = Collection["val5"];
                }
            }

            return searchBuilder.ToString();
        }

        private bool ValidateSearchFields(FormCollection Collection)
        {
            if (string.IsNullOrEmpty(Collection["col1"]) || Collection["col1"] == "undefined" ||
               string.IsNullOrEmpty(Collection["val1"]) || Collection["val1"] == "undefined")
            {
                return false;
            }
            return true;
        }

        private void PopulateDropDownlist(out List<SelectListItem> SearchColumns, string SelectedValue, List<KeyValuePair<int, string>> Columns)
        {
            SearchColumns = new List<SelectListItem>();
            foreach (var item in Columns)
            {
                SelectListItem newSelectListItem = new SelectListItem { Text = item.Value, Value = item.Value, Selected = item.Value == SelectedValue };
                SearchColumns.Add(newSelectListItem);
            }
        }



        /// <summary>
        /// Following Action method takes ResponseId as a parameter and deletes the response.
        /// For now it returns nothing as a confirmation of deletion, we may add some error/success
        /// messages later. TBD
        /// </summary>
        /// <param name="ResponseId"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Delete(string ResponseId)
        {
            SurveyAnswerRequest SARequest = new SurveyAnswerRequest();
            SARequest.SurveyAnswerList.Add(new SurveyAnswerDTO() { ResponseId = ResponseId });
            string Id = Session["UserId"].ToString();
            SARequest.Criteria.UserId = SurveyHelper.GetDecryptUserId(Id);
            SARequest.Criteria.IsSqlProject = (bool)Session["IsSqlProject"];
            SARequest.Criteria.SurveyId = Session["RootFormId"].ToString();
            SurveyAnswerResponse SAResponse = _isurveyFacade.DeleteResponse(SARequest);

            return Json(string.Empty);


        }


        private Epi.Web.Enter.Common.DTO.SurveyAnswerDTO GetCurrentSurveyAnswer()
        {
            Epi.Web.Enter.Common.DTO.SurveyAnswerDTO result = null;

            if (TempData.ContainsKey(Epi.Web.MVC.Constants.Constant.RESPONSE_ID)
                && TempData[Epi.Web.MVC.Constants.Constant.RESPONSE_ID] != null
                && !string.IsNullOrEmpty(TempData[Epi.Web.MVC.Constants.Constant.RESPONSE_ID].ToString())
                )
            {
                string responseId = TempData[Epi.Web.MVC.Constants.Constant.RESPONSE_ID].ToString();

                //TODO: Now repopulating the TempData (by reassigning to responseId) so it persisits, later we will need to find a better 
                //way to replace it. 
                TempData[Epi.Web.MVC.Constants.Constant.RESPONSE_ID] = responseId;
                return _isurveyFacade.GetSurveyAnswerResponse(responseId).SurveyResponseList[0];
            }

            return result;
        }



        public SurveyInfoModel GetSurveyInfo(string SurveyId)
        {
            SurveyInfoModel surveyInfoModel = _isurveyFacade.GetSurveyInfoModel(SurveyId);
            return surveyInfoModel;
        }

        public List<FormInfoModel> GetFormsInfoList(Guid UserId, int OrgID)
        {
            FormsInfoRequest formReq = new FormsInfoRequest();

            formReq.Criteria.UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());//Hard coded user for now.
            formReq.Criteria.CurrentOrgId = OrgID;
            // formReq.Criteria.UserId = UserId;
            //define filter criteria here.
            //define sorting criteria here.

            List<FormInfoModel> listOfFormsInfoModel = _isurveyFacade.GetFormsInfoModelList(formReq);



            // return listOfFormsInfoModel.Where(x=>x.OrganizationId== OrgID).ToList();
            return listOfFormsInfoModel;
        }

        private int Compare(KeyValuePair<int, string> a, KeyValuePair<int, string> b)
        {
            return a.Key.CompareTo(b.Key);
        }


        public FormResponseInfoModel GetFormResponseInfoModel(string SurveyId, int PageNumber, string sort = "", string sortfield = "" ,int orgid =-1)
        {
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            FormResponseInfoModel FormResponseInfoModel = new FormResponseInfoModel();
            FormResponseInfoModel.SearchModel = new SearchBoxModel();
            SurveyResponseXML SurveyResponseXML = new SurveyResponseXML();
            if (!string.IsNullOrEmpty(SurveyId))
            {
                SurveyAnswerRequest FormResponseReq = new SurveyAnswerRequest();
                FormSettingRequest FormSettingReq = new Enter.Common.Message.FormSettingRequest();
               
                //Populating the request

                FormSettingReq.FormInfo.FormId = SurveyId;
                FormSettingReq.FormInfo.UserId = UserId;
                //Getting Column Name  List
                FormSettingReq.CurrentOrgId = orgid;
                FormSettingReq.FormInfoOnly = true;
                FormSettingResponse FormSettingResponse = _isurveyFacade.GetFormSettings(FormSettingReq);   //Pain Point
                Columns = FormSettingResponse.FormSetting.ColumnNameList.ToList();
                Columns.Sort(Compare);

                // Setting  Column Name  List
                FormResponseInfoModel.Columns = Columns;

                FormResponseInfoModel.FormInfoModel.IsShared = FormSettingResponse.FormInfo.IsShared;
                FormResponseInfoModel.FormInfoModel.IsShareable = FormSettingResponse.FormInfo.IsShareable; 
                // Set User Role 
                //if (FormResponseInfoModel.FormInfoModel.IsShared)
                //{

                //    SetUserRole(UserId, orgid);
                //}
                //else
                //{
                //SetUserRole(UserId, FormSettingResponse.FormInfo.OrganizationId);
                //}
                SetUserRole(UserId, orgid);
              
                FormResponseReq.Criteria.SurveyId = SurveyId.ToString();
                FormResponseReq.Criteria.PageNumber = PageNumber;
                FormResponseReq.Criteria.UserId = UserId;
                FormResponseReq.Criteria.IsSqlProject = FormSettingResponse.FormInfo.IsSQLProject;
                FormResponseReq.Criteria.IsShareable = FormSettingResponse.FormInfo.IsShareable;
                FormResponseReq.Criteria.UserOrganizationId = orgid;
                Session["IsSqlProject"] = FormSettingResponse.FormInfo.IsSQLProject;
                Session["IsOwner"] = FormSettingResponse.FormInfo.IsOwner;
                //if (Session["SearchCriteria"] != null)
                //{
                //    FormResponseInfoModel.SearchModel = (SearchBoxModel)Session["SearchCriteria"];
                //}
                // Following code retain search starts
                if (Session["SearchCriteria"] != null &&
                    !string.IsNullOrEmpty(Session["SearchCriteria"].ToString()) &&
                    (Request.QueryString["col1"] == null || Request.QueryString["col1"] == "undefined"))
                {
                    FormResponseReq.Criteria.SearchCriteria = Session["SearchCriteria"].ToString();
                    FormResponseInfoModel.SearchModel = (SearchBoxModel)Session["SearchModel"];
                }
                else
                {
                    FormResponseReq.Criteria.SearchCriteria = CreateSearchCriteria(Request.QueryString, FormResponseInfoModel.SearchModel, FormResponseInfoModel);
                    Session["SearchModel"] = FormResponseInfoModel.SearchModel;
                    Session["SearchCriteria"] = FormResponseReq.Criteria.SearchCriteria;
                }
                // Following code retain search ends
                PopulateDropDownlists(FormResponseInfoModel, FormSettingResponse.FormSetting.FormControlNameList.ToList());

                if (sort.Length > 0)
                {
                    FormResponseReq.Criteria.SortOrder = sort;
                }
                if (sortfield.Length > 0)
                {
                    FormResponseReq.Criteria.Sortfield = sortfield;
                }


                SurveyAnswerResponse FormResponseList = _isurveyFacade.GetFormResponseList(FormResponseReq);//Pain Point


                //var ResponseTableList ; //= FormSettingResponse.FormSetting.DataRows;
                //Setting Resposes List
                List<ResponseModel> ResponseList = new List<ResponseModel>();
                foreach (var item in FormResponseList.SurveyResponseList)
                {
                    if (item.SqlData != null)
                    {
                        ResponseList.Add(ConvertRowToModel(item, Columns));
                    }
                    else
                    {
                        ResponseList.Add(SurveyResponseXML.ConvertXMLToModel(item, Columns));
                    }

                }


                //foreach (var item in FormResponseList.SurveyResponseList)
                //{
                //    ResponseList.Add(SurveyResponseXML.ConvertXMLToModel(item, Columns));
                //}

                FormResponseInfoModel.ResponsesList = ResponseList;
                //Setting Form Info 
                FormResponseInfoModel.FormInfoModel = Mapper.ToFormInfoModel(FormResponseList.FormInfo);
                //Setting Additional Data

                FormResponseInfoModel.NumberOfPages = FormResponseList.NumberOfPages;
                FormResponseInfoModel.PageSize = ReadPageSize();
                FormResponseInfoModel.NumberOfResponses = FormResponseList.NumberOfResponses;
                FormResponseInfoModel.sortfield = sortfield;
                FormResponseInfoModel.sortOrder = sort;
                FormResponseInfoModel.CurrentPage = PageNumber;
            }
            return FormResponseInfoModel;
        }

        private void SetUserRole(int UserId, int OrgId)
        {
            UserRequest UserRequest = new UserRequest();
            UserRequest.Organization.OrganizationId = OrgId;
            UserRequest.User.UserId = UserId;
            var UserRes = _isurveyFacade.GetUserInfo(UserRequest);
            if (UserRes.User.Count() > 0)
            {
                Session["UsertRole"] = UserRes.User[0].Role;
            }
        }

        private void PopulateDropDownlists(FormResponseInfoModel FormResponseInfoModel, List<KeyValuePair<int, string>> list)
        {
            PopulateDropDownlist(out FormResponseInfoModel.SearchColumns1, FormResponseInfoModel.SearchModel.SearchCol1, list);
            PopulateDropDownlist(out FormResponseInfoModel.SearchColumns2, FormResponseInfoModel.SearchModel.SearchCol2, list);
            PopulateDropDownlist(out FormResponseInfoModel.SearchColumns3, FormResponseInfoModel.SearchModel.SearchCol3, list);
            PopulateDropDownlist(out FormResponseInfoModel.SearchColumns4, FormResponseInfoModel.SearchModel.SearchCol4, list);
            PopulateDropDownlist(out FormResponseInfoModel.SearchColumns5, FormResponseInfoModel.SearchModel.SearchCol5, list);
        }


        private ResponseModel ConvertRowToModel(SurveyAnswerDTO item, List<KeyValuePair<int, string>> Columns)
        {
            ResponseModel Response = new ResponseModel();

            Response.Column0 = item.SqlData["GlobalRecordId"];
            if (Columns.Count > 0)
            {
                Response.Column1 = item.SqlData[Columns[0].Value];
            }

            if (Columns.Count > 1)
            {
                Response.Column2 = item.SqlData[Columns[1].Value];
            }

            if (Columns.Count > 2)
            {
                Response.Column3 = item.SqlData[Columns[2].Value];
            }
            if (Columns.Count > 3)
            {
                Response.Column4 = item.SqlData[Columns[3].Value];
            }
            if (Columns.Count > 4)
            {
                Response.Column5 = item.SqlData[Columns[4].Value];
            }

            //Response.Column2 = item.SqlData[Columns[2].Value];
            //Response.Column3 = item.SqlData[Columns[3].Value];
            //Response.Column4 = item.SqlData[Columns[4].Value];
            //Response.Column5 = item.SqlData[Columns[5].Value];

            return Response;
        }

        private int ReadPageSize()
        {
            return Convert.ToInt16(WebConfigurationManager.AppSettings["RESPONSE_PAGE_SIZE"].ToString());
        }

        //  [HttpPost]

        //    public ActionResult Edit(string ResId)
        //    {
        ////    Epi.Web.Enter.Common.DTO.SurveyAnswerDTO surveyAnswerDTO = GetSurveyAnswer(ResId);

        //    return RedirectToAction(Epi.Web.MVC.Constants.Constant.INDEX, Epi.Web.MVC.Constants.Constant.SURVEY_CONTROLLER, new { responseid = ResId, PageNumber = 1 });
        //    }
        private Epi.Web.Enter.Common.DTO.SurveyAnswerDTO GetSurveyAnswer(string responseId, string FormId)
        {
            Epi.Web.Enter.Common.DTO.SurveyAnswerDTO result = null;
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            //responseId = TempData[Epi.Web.MVC.Constants.Constant.RESPONSE_ID].ToString();
            var SurveyAnswerResponse = _isurveyFacade.GetSurveyAnswerResponse(responseId, FormId, UserId);
            result = SurveyAnswerResponse.SurveyResponseList[0];
            result.FormOwnerId = SurveyAnswerResponse.FormInfo.OwnerId;
            return result;

        }

        [HttpGet]
        public ActionResult LogOut()
        {

            FormsAuthentication.SignOut();
            this.Session.Clear();
            return RedirectToAction("Index", "Login");
        }

        [HttpGet]
        public ActionResult GetSettings(string formid)//List<FormInfoModel> ModelList, string formid)
        {
            FormSettingRequest FormSettingReq = new Enter.Common.Message.FormSettingRequest();
            List<KeyValuePair<int, string>> TempColumns = new List<KeyValuePair<int, string>>();
            //Get All forms
            List<FormsHierarchyDTO> FormsHierarchy = GetFormsHierarchy(formid);
            // List<FormSettingResponse> FormSettingResponseList = new List<FormSettingResponse>();
            List<SettingsInfoModel> ModelList = new List<SettingsInfoModel>();
            foreach (var Item in FormsHierarchy)
            {
                FormSettingReq.GetXml = true;
                FormSettingReq.FormInfo.FormId = new Guid(Item.FormId).ToString();
                FormSettingReq.FormInfo.UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
                FormSettingReq.CurrentOrgId = int.Parse(Session["SelectedOrgId"].ToString());
                //Getting Column Name  List

                FormSettingResponse FormSettingResponse = _isurveyFacade.GetFormSettings(FormSettingReq);
                //  FormSettingResponseList.Add(FormSettingResponse);



                // FormSettingResponse FormSettingResponse = _isurveyFacade.GetFormSettings(FormSettingReq);
                Columns = FormSettingResponse.FormSetting.ColumnNameList.ToList();
                TempColumns = Columns;
                Columns.Sort(Compare);


                Dictionary<int, string> dictionary = Columns.ToDictionary(pair => pair.Key, pair => pair.Value);
                SettingsInfoModel Model = new SettingsInfoModel();
                Model.SelectedControlNameList = dictionary;

                Columns = FormSettingResponse.FormSetting.FormControlNameList.ToList();
                // Get Additional Metadata columns 
                if (!FormSettingResponse.FormInfo.IsSQLProject)
                {
                    var MetaDataColumns = Epi.Web.MVC.Constants.Constant.MetaDaTaColumnNames();
                    Dictionary<int, string> Columndictionary = TempColumns.ToDictionary(pair => pair.Key, pair => pair.Value);

                    foreach (var item in MetaDataColumns)
                    {

                        if (!Columndictionary.ContainsValue(item))
                        {
                            Columns.Add(new KeyValuePair<int, string>(Columns.Count() + 1, item));
                        }

                    }

                    Columns.Sort(Compare);
                }

                Dictionary<int, string> dictionary1 = Columns.ToDictionary(pair => pair.Key, pair => pair.Value);

                Model.FormControlNameList = dictionary1;




                Columns = FormSettingResponse.FormSetting.AssignedUserList.ToList();
                if (Columns.Exists(col => col.Value == Session["UserEmailAddress"].ToString()))
                {
                    Columns.Remove(Columns.First(u => u.Value == Session["UserEmailAddress"].ToString()));
                }

                //Columns.Sort(Compare);

                Dictionary<int, string> dictionary2 = Columns.ToDictionary(pair => pair.Key, pair => pair.Value);

                Model.AssignedUserList = dictionary2;





                Columns = FormSettingResponse.FormSetting.UserList.ToList();

                if (Columns.Exists(col => col.Value == Session["UserEmailAddress"].ToString()))
                {
                    Columns.Remove(Columns.First(u => u.Value == Session["UserEmailAddress"].ToString()));
                }
                //Columns.Sort(Compare);

                Dictionary<int, string> dictionary3 = Columns.ToDictionary(pair => pair.Key, pair => pair.Value);

                Model.UserList = dictionary3;

                Columns = FormSettingResponse.FormSetting.AvailableOrgList.ToList();
                Dictionary<int, string> dictionary4 = Columns.ToDictionary(pair => pair.Key, pair => pair.Value);
                Model.AvailableOrgList = dictionary4;

                Columns = FormSettingResponse.FormSetting.SelectedOrgList.ToList();
                Dictionary<int, string> dictionary5 = Columns.ToDictionary(pair => pair.Key, pair => pair.Value);
                Model.SelectedOrgList = dictionary5;

                Model.IsShareable = FormSettingResponse.FormInfo.IsShareable; 
                Model.IsDraftMode = FormSettingResponse.FormInfo.IsDraftMode;
                Model.FormOwnerFirstName = FormSettingResponse.FormInfo.OwnerFName;
                Model.FormOwnerLastName = FormSettingResponse.FormInfo.OwnerLName;
                Model.FormName = FormSettingResponse.FormInfo.FormName;
                Model.FormId = Item.FormId;
                Model.DataAccessRuleIds = FormSettingResponse.FormSetting.DataAccessRuleIds;
                Model.SelectedDataAccessRule = FormSettingResponse.FormSetting.SelectedDataAccessRule;
                Model.HasDraftModeData = FormSettingResponse.FormInfo.HasDraftModeData;
                var  DataAccessRuleDescription = "" ;
                foreach (var item in FormSettingResponse.FormSetting.DataAccessRuleDescription)
                {
                    DataAccessRuleDescription = DataAccessRuleDescription + item.Key.ToString() + " : " + item.Value + "\n";
                }

                Model.DataAccessRuleDescription = DataAccessRuleDescription;        
                ModelList.Add(Model);
            }
            return PartialView("Settings", ModelList);

        }
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult CheckForConcurrency(String ResponseId) 
        {
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            Epi.Web.Enter.Common.DTO.SurveyAnswerDTO surveyAnswerDTO = GetSurveyAnswer(ResponseId, Session["RootFormId"].ToString());
            surveyAnswerDTO.LoggedInUserId = UserId;
            Session["EditForm"] = ResponseId;
            //Session[""]
            return Json(surveyAnswerDTO);
        
        }
        
       [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult Notify(String ResponseId) 
        {
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            
           //Get current user info
            int CurrentOrgId = int.Parse(Session["SelectedOrgId"].ToString());
            var UserInfo = _isurveyFacade.GetUserInfo(UserId);
           //Get Organization admin info 
            Epi.Web.Enter.Common.DTO.SurveyAnswerDTO surveyAnswerDTO = GetSurveyAnswer(ResponseId, Session["RootFormId"].ToString());
            SurveyInfoModel surveyInfoModel = GetSurveyInfo(surveyAnswerDTO.SurveyId);

            var OwnerInfo = _isurveyFacade.GetUserInfo(surveyAnswerDTO.FormOwnerId); ;
            

                Epi.Web.Enter.Common.Email.Email EmilObj = new Enter.Common.Email.Email();
                 //ResponseId;

                EmilObj.Subject = "Record locked notification.";
                EmilObj.Body = " A user was unable to edit/delete a Epi Info™ Cloud Data Capture recored. \n \n Please login to Epi Info™ Cloud Data Capture system to Unlock this record.\n \n Below are the needed info to unlock the record.\n \n Response id: " + ResponseId + "\n\n User email: " + UserInfo.User.EmailAddress+ "\n\n";
                EmilObj.From = ConfigurationManager.AppSettings["EMAIL_FROM"];
                EmilObj.To =  new List<string>();
                EmilObj.To.Add(OwnerInfo.User.EmailAddress);

                var success = Epi.Web.Enter.Common.Email.EmailHandler.SendMessage(EmilObj);
 
           
            return Json(1);
        
        }
        //Unlock

       [HttpPost]
       [AcceptVerbs(HttpVerbs.Post)]
       public ActionResult Unlock(String ResponseId, bool RecoverLastRecordVersion)
       {
           try
           {
               int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
               SurveyAnswerRequest SurveyAnswerRequest = new SurveyAnswerRequest();
               SurveyAnswerRequest.SurveyAnswerList.Add(new SurveyAnswerDTO() { ResponseId = ResponseId });
               SurveyAnswerRequest.Criteria.StatusId = 2;
               SurveyAnswerRequest.Criteria.SurveyAnswerIdList.Add(ResponseId);
               SurveyAnswerRequest.Criteria.UserId = UserId;
               Session["RecoverLastRecordVersion"] = RecoverLastRecordVersion;
               _isurveyFacade.UpdateResponseStatus(SurveyAnswerRequest);
           }
           catch (Exception ex)
           {

               return Json("Erorr");

           }
           return Json("Success");

       }
        [HttpPost]
        [AcceptVerbs(HttpVerbs.Post)]
        public ActionResult SaveSettings(string formid)
        {
            int CurrentOrgId = int.Parse(Session["SelectedOrgId"].ToString());
            List<FormsHierarchyDTO> FormList = GetFormsHierarchy(formid);
            FormSettingRequest FormSettingReq = new Enter.Common.Message.FormSettingRequest();
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            foreach (var Form in FormList)
            {
                FormSettingReq.GetXml = true;
                FormSettingReq.FormInfo.FormId = new Guid(formid).ToString();
                FormSettingReq.FormInfo.UserId = UserId;
                FormSettingDTO FormSetting = new FormSettingDTO();
                FormSetting.FormId = Form.FormId;
                FormSetting.ColumnNameList = GetDictionary(this.Request.Form["SelectedColumns_" + Form.FormId]);
                FormSetting.AssignedUserList = GetDictionary(this.Request.Form["SelectedUser"]);
                FormSetting.SelectedOrgList = GetDictionary(this.Request.Form["SelectedOrg"]);
                FormSetting.IsShareable = GetBoolValue(this.Request.Form["IsShareable"]);
                FormSetting.SelectedDataAccessRule = int.Parse(this.Request.Form["DataAccessRuleId"]);
                
                if (!string.IsNullOrEmpty(this.Request.Form["SoftDeleteForm"]) && this.Request.Form["SoftDeleteForm"].ToUpper() == "ON")
                {
                    FormSetting.IsDisabled = true;
                }
                if (!string.IsNullOrEmpty(this.Request.Form["RemoveTestData"]) && this.Request.Form["RemoveTestData"].ToUpper() == "ON")
                {
                    FormSetting.DeleteDraftData = true;
                }
                FormSettingReq.FormSetting.Add(FormSetting);
                FormSettingReq.FormInfo.IsDraftMode = GetBoolValue(this.Request.Form["Mode"]);
                FormSettingReq.CurrentOrgId = CurrentOrgId;
            }
            FormSettingResponse FormSettingResponse = _isurveyFacade.SaveSettings(FormSettingReq);



            bool IsMobileDevice = this.Request.Browser.IsMobileDevice;

            var model = new FormResponseInfoModel();

           
            model = GetFormResponseInfoModel(formid, 1, "", "", CurrentOrgId);

            if (IsMobileDevice == false)
            {
                if (!string.IsNullOrEmpty(this.Request.Form["SoftDeleteForm"]) && this.Request.Form["SoftDeleteForm"].ToUpper() == "ON")
                {
                    return Json(null);
                }
                else {
                    return PartialView("ListResponses", model);
                }
            }
            else
            {
                return View("ListResponses", model);
            }




        }



        public Dictionary<int, string> GetDictionary(string List)
        {
            Dictionary<int, string> Dictionary = new Dictionary<int, string>();
            if (!string.IsNullOrEmpty(List))
            {
                Dictionary = List.Split(',').ToList().Select((s, i) => new { s, i }).ToDictionary(x => x.i, x => x.s);
            }
            return Dictionary;
        }
        public bool GetBoolValue(string value)
        {
            bool BoolValue = false;
            if (!string.IsNullOrEmpty(value))
            {
                int val = int.Parse(value);
                if (val == 1)
                {
                    BoolValue = true;
                }
            }


            return BoolValue;
        }
       

        private List<FormsHierarchyDTO> GetFormsHierarchy(string formid)
        {
            FormsHierarchyResponse FormsHierarchyResponse = new FormsHierarchyResponse();
            FormsHierarchyRequest FormsHierarchyRequest = new FormsHierarchyRequest();

            FormsHierarchyRequest.SurveyInfo.FormId = formid;
            // FormsHierarchyRequest.SurveyResponseInfo.ResponseId = Session["RootResponseId"].ToString();
            FormsHierarchyResponse = _isurveyFacade.GetFormsHierarchy(FormsHierarchyRequest);

            return FormsHierarchyResponse.FormsHierarchy;
        }
       
    }
}

﻿using System;
using System.Web.Mvc;
using Epi.Web.MVC.Facade;
using Epi.Web.MVC.Models;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Epi.Core.EnterInterpreter;
using System.Web.Security;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Configuration;
using System.Web.Routing;
using System.Web.WebPages;
using System.Web.Caching;
using System.Reflection;
using System.Diagnostics;
using System.Reflection;
using System.Diagnostics;
using Epi.Web.Common.Message;
using Epi.Web.Common.DTO;
using Epi.Web.MVC.Utility;
using System.Linq;
using System.Web.Configuration;
namespace Epi.Web.MVC.Controllers
    {
    [Authorize]
    public class SurveyController : Controller
        {




        //declare SurveyTransactionObject object
        private ISurveyFacade _isurveyFacade;
        /// <summary>
        /// Injectinting SurveyTransactionObject through Constructor
        /// </summary>
        /// <param name="iSurveyInfoRepository"></param>
        private IEnumerable<XElement> PageFields;
        private string RequiredList = "";
        private string RootFormId = "";
        private string RootResponseId = "";
        private bool IsEditMode;
        private List<SurveyAnswerDTO> ListSurveyAnswerDTO = new List<SurveyAnswerDTO>();
        private int ReffererPageNum;
        List<KeyValuePair<int, string>> Columns = new List<KeyValuePair<int, string>>();
        public SurveyController(ISurveyFacade isurveyFacade)
            {
            _isurveyFacade = isurveyFacade;
            }




        /// <summary>
        /// create the new resposeid and put it in temp data. create the form object. create the first survey response
        /// </summary>
        /// <param name="surveyId"></param>
        /// <returns></returns>

        [HttpGet]

        //  [OutputCache(NoStore = true, Duration = 0, VaryByParam = "None")] 
        public ActionResult Index(string responseId, int PageNumber = 1, string Edit = "", string FormValuesHasChanged ="")
            {
            SetGlobalVariable();

            //For child to read Data from parent
            SurveyAnswerRequest SurveyAnswerRequest = new SurveyAnswerRequest();
            SurveyAnswerRequest.Criteria.SurveyAnswerIdList.Add(responseId);
            SurveyAnswerResponse SurveyAnswerResponseList = _isurveyFacade.GetAncestorResponses(SurveyAnswerRequest);

            try
                {
                
                string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                ViewBag.Version = version;
                ViewBag.Edit = Edit;
                bool IsMobileDevice = false;
                IsMobileDevice = this.Request.Browser.IsMobileDevice;
                if (IsMobileDevice == false)
                    {
                    IsMobileDevice = Epi.Web.MVC.Utility.SurveyHelper.IsMobileDevice(this.Request.UserAgent.ToString());
                    }

                Epi.Web.Common.DTO.SurveyAnswerDTO surveyAnswerDTO = GetSurveyAnswer(responseId);
                if (!string.IsNullOrEmpty(Edit))
                    {

                    Session["RootResponseId"] = responseId;
                    Session["RootFormId"] = surveyAnswerDTO.SurveyId;
                    }
                List<FormsHierarchyDTO> FormsHierarchy = GetFormsHierarchy();
                // SurveyInfoModel surveyInfoModel = _isurveyFacade.GetSurveyInfoModel(surveyAnswerDTO.SurveyId);
                SurveyInfoModel surveyInfoModel = GetSurveyInfo(surveyAnswerDTO.SurveyId);
                PreValidationResultEnum ValidationTest = PreValidateResponse(Mapper.ToSurveyAnswerModel(surveyAnswerDTO), surveyInfoModel);
                if (PageNumber == 0)
                    {
                    PageNumber = GetSurveyPageNumber(surveyAnswerDTO.XML.ToString());

                    }
                else
                    {


                    }


                switch (ValidationTest)
                    {
                    case PreValidationResultEnum.SurveyIsPastClosingDate:
                        return View("SurveyClosedError");
                    case PreValidationResultEnum.SurveyIsAlreadyCompleted:
                        return View("IsSubmitedError");
                    case PreValidationResultEnum.Success:
                    default:

                        
                       


                        var form = _isurveyFacade.GetSurveyFormData(surveyAnswerDTO.SurveyId, PageNumber, surveyAnswerDTO, IsMobileDevice, SurveyAnswerResponseList.SurveyResponseList);
                        //var form = _isurveyFacade.GetSurveyFormData1(surveyAnswerDTO.SurveyId, responseId, PageNumber,temp.SurveyResponseList, IsMobileDevice);
                        ////////////////Assign data to a child
                        TempData["Width"] = form.Width + 5;
                        // if redirect then perform server validation before displaying
                        if (TempData.ContainsKey("isredirect") && !string.IsNullOrWhiteSpace(TempData["isredirect"].ToString()))
                            {
                            form.Validate(form.RequiredFieldsList);
                            }
                        //if (string.IsNullOrEmpty(Edit))
                        //    {
                        //    surveyAnswerDTO.IsDraftMode = surveyInfoModel.IsDraftMode;

                        //    }
                       if(string.IsNullOrEmpty(Edit)){
                            this.SetCurrentPage(surveyAnswerDTO, PageNumber);
                           }
                        //PassCode start
                        if (IsMobileDevice)
                            {
                            form = SetFormPassCode(form, responseId);
                            }
                        form.StatusId = surveyAnswerDTO.Status;
                        if (!string.IsNullOrEmpty(Edit))
                            {
                            if (surveyAnswerDTO.IsDraftMode)
                                {
                                form.IsDraftModeStyleClass = "draft";
                                }
                            }
                        if (Session["FormValuesHasChanged"] != null)
                            {
                            form.FormValuesHasChanged = Session["FormValuesHasChanged"].ToString();
                            }
                        form.RequiredFieldsList = this.RequiredList;
                        //passCode end
                        SurveyModel SurveyModel = new SurveyModel();
                        SurveyModel.Form = form;
                        SurveyModel.RelateModel =Mapper.ToRelateModel(FormsHierarchy,form.SurveyInfo.SurveyId);
                        return View(Epi.Web.MVC.Constants.Constant.INDEX_PAGE, SurveyModel);
                    }
                }

            catch (Exception ex)
                {

                Epi.Web.Utility.ExceptionMessage.SendLogMessage(ex, this.HttpContext);

                return View(Epi.Web.MVC.Constants.Constant.EXCEPTION_PAGE);
                }
            //}
            //return null;
            }

     
        [HttpPost]
        //  [OutputCache(NoStore = true, Duration = 0, VaryByParam = "None")]
        [ValidateAntiForgeryToken]
        //public ActionResult Index(SurveyInfoModel surveyInfoModel, string Submitbutton, string Savebutton, string ContinueButton, string PreviousButton, int PageNumber = 1)
        public ActionResult Index(SurveyAnswerModel surveyAnswerModel, string Submitbutton, string Savebutton, string ContinueButton, string PreviousButton, string Close, string CloseButton, int PageNumber = 0, string Form_Has_Changed = "", string Requested_View_Id = "")
            {

            //var Form_Has_Changed = this.Request.Form["Form_Has_Changed"].ToString();
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            ViewBag.Version = version;
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());

            Session["FormValuesHasChanged"] = Form_Has_Changed;
            
            //For child to read Data from parent
            SurveyAnswerRequest SurveyAnswerRequest = new SurveyAnswerRequest();
            SurveyAnswerRequest.Criteria.SurveyAnswerIdList.Add(surveyAnswerModel.ResponseId);
            SurveyAnswerResponse SurveyAnswerResponseList = _isurveyFacade.GetAncestorResponses(SurveyAnswerRequest);


            SetGlobalVariable();

            List<FormsHierarchyDTO> FormsHierarchy = GetFormsHierarchy();
            string responseId = surveyAnswerModel.ResponseId;
            bool IsMobileDevice = false;
            IsMobileDevice = this.Request.Browser.IsMobileDevice;
            if (IsMobileDevice == false)
                {
                IsMobileDevice = Epi.Web.MVC.Utility.SurveyHelper.IsMobileDevice(this.Request.UserAgent.ToString());
                }
            try
                {
                string FormValuesHasChanged = Form_Has_Changed;
                // bool.TryParse(this.Request.Form["Form_Has_Changed"], out FormValuesHasChanged);
                Epi.Web.Common.DTO.SurveyAnswerDTO SurveyAnswer = _isurveyFacade.GetSurveyAnswerResponse(responseId).SurveyResponseList[0];
                 
                // SurveyInfoModel surveyInfoModel = _isurveyFacade.GetSurveyInfoModel(SurveyAnswer.SurveyId);
                object temp = System.Web.HttpContext.Current.Cache;
                SurveyInfoModel surveyInfoModel = GetSurveyInfo(SurveyAnswer.SurveyId);

                //////////////////////UpDate Survey Mode//////////////////////////
                SurveyAnswer.IsDraftMode = surveyInfoModel.IsDraftMode;
                PreValidationResultEnum ValidationTest = PreValidateResponse(Mapper.ToSurveyAnswerModel(SurveyAnswer), surveyInfoModel);

                switch (ValidationTest)
                    {
                    case PreValidationResultEnum.SurveyIsPastClosingDate:
                        return View("SurveyClosedError");
                    case PreValidationResultEnum.SurveyIsAlreadyCompleted:
                        return View("IsSubmitedError");
                    case PreValidationResultEnum.Success:
                    default:
                        

                        //Update Survey Model Start
                        MvcDynamicForms.Form form = UpDateSurveyModel(surveyInfoModel, IsMobileDevice, FormValuesHasChanged, SurveyAnswer);
                        //Update Survey Model End

                        //PassCode start
                        if (IsMobileDevice)
                            {

                            form = SetFormPassCode(form, responseId);
                            }
                        //passCode end
                        form.StatusId = SurveyAnswer.Status;
                        bool IsSubmited = false;
                        bool IsSaved = false;

                        form = SetLists(form);
                      
                        _isurveyFacade.UpdateSurveyResponse(surveyInfoModel, responseId, form, SurveyAnswer, IsSubmited, IsSaved, PageNumber, UserId);


                        if (!string.IsNullOrEmpty(this.Request.Form["is_save_action"]) && this.Request.Form["is_save_action"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase))
                            {
                           

                            form = SaveCurrentForm(form, surveyInfoModel, SurveyAnswer, responseId, UserId, IsSubmited, IsSaved, IsMobileDevice, FormValuesHasChanged, PageNumber);
                            form = SetLists(form);
                            TempData["Width"] = form.Width + 5;
                            SurveyModel SurveyModel = new SurveyModel();
                            SurveyModel.Form = form;
                            SurveyModel.RelateModel = Mapper.ToRelateModel(FormsHierarchy, form.SurveyInfo.SurveyId);
                           
                            return View(Epi.Web.MVC.Constants.Constant.INDEX_PAGE, SurveyModel);

                            }
                        else if (!string.IsNullOrEmpty(this.Request.Form["Get_Child_action"]) && this.Request.Form["Get_Child_action"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase))
                            {
                            int RequestedViewId;
                           RequestedViewId = int.Parse(this.Request.Form["Requested_View_Id"]);
                           form = SaveCurrentForm(form, surveyInfoModel, SurveyAnswer, responseId, UserId, IsSubmited, IsSaved, IsMobileDevice, FormValuesHasChanged, PageNumber);
                           form = SetLists(form);
                           TempData["Width"] = form.Width + 5;
                           Session["RequestedViewId"] = RequestedViewId;
                            SurveyModel SurveyModel = new SurveyModel();
                            SurveyModel.Form = form;
                            SurveyModel.RelateModel = Mapper.ToRelateModel(FormsHierarchy, form.SurveyInfo.SurveyId);
                            SurveyModel.RequestedViewId = RequestedViewId ;
                            int.TryParse(this.Request.Form["Requested_View_Id"].ToString(),out RequestedViewId);
                            var RelateSurveyId = FormsHierarchy.Single(x => x.ViewId == RequestedViewId);
                           
                            int ViewId = int.Parse(Requested_View_Id);
                            string ChildResponseId = AddNewChild(surveyInfoModel.SurveyId, ViewId, responseId, FormValuesHasChanged, "1");
                            return RedirectToRoute(new { Controller = "Survey", Action = "Index", responseid = ChildResponseId, PageNumber = 1 });

                            }
                        else if (!string.IsNullOrEmpty(this.Request.Form["is_goto_action"]) && this.Request.Form["is_goto_action"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase))
                            {
                            //This is a Navigation to a url


                            form = SetLists(form);

                            _isurveyFacade.UpdateSurveyResponse(surveyInfoModel, responseId, form, SurveyAnswer, IsSubmited, IsSaved, PageNumber, UserId);

                            SurveyAnswer = _isurveyFacade.GetSurveyAnswerResponse(responseId).SurveyResponseList[0];
                            form = _isurveyFacade.GetSurveyFormData(surveyInfoModel.SurveyId, PageNumber, SurveyAnswer, IsMobileDevice, SurveyAnswerResponseList.SurveyResponseList);
                            form.FormValuesHasChanged = FormValuesHasChanged;
                            TempData["Width"] = form.Width + 5;
                            SurveyModel SurveyModel = new SurveyModel();
                            SurveyModel.Form = form;
                            SurveyModel.RelateModel = Mapper.ToRelateModel(FormsHierarchy, form.SurveyInfo.SurveyId);
                           
                            return View(Epi.Web.MVC.Constants.Constant.INDEX_PAGE, SurveyModel);
                            
                            }

                        else if (form.Validate(form.RequiredFieldsList))
                            {
                            if (!string.IsNullOrEmpty(Submitbutton) || !string.IsNullOrEmpty(CloseButton) || (!string.IsNullOrEmpty(this.Request.Form["is_save_action_Mobile"]) && this.Request.Form["is_save_action_Mobile"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase)))
                                {
                                KeyValuePair<string, int> ValidateValues = ValidateAll(form, UserId, IsSubmited, IsSaved, IsMobileDevice, FormValuesHasChanged);
                                if (!string.IsNullOrEmpty(ValidateValues.Key) && !string.IsNullOrEmpty(ValidateValues.Value.ToString()))
                                    {
                                    return RedirectToRoute(new { Controller = "Survey", Action = "Index", responseid = ValidateValues.Key, PageNumber = ValidateValues.Value.ToString() });
                                    }
                                //SurveyAnswerRequest SurveyAnswerRequest = new SurveyAnswerRequest();
                                //SurveyAnswerResponse Object = _isurveyFacade.GetSurveyAnswerHierarchy(SurveyAnswerRequest);
                                var List = ListSurveyAnswerDTO.OrderByDescending(x=>x.DateCreated);//.OrderBy(x => x.ParentRecordId);
                                foreach (var Obj in List)
                                    {
                               // SurveyAnswerDTO SurveyAnswer2 = _isurveyFacade.GetSurveyAnswerResponse(RootResponseId).SurveyResponseList[0];
                              //  SurveyInfoModel surveyInfoModel2 = GetSurveyInfo(RootFormId);
                                    SurveyAnswerDTO SurveyAnswer2 = _isurveyFacade.GetSurveyAnswerResponse(Obj.ResponseId).SurveyResponseList[0];
                                    SurveyInfoModel surveyInfoModel2 = GetSurveyInfo(Obj.SurveyId);
                                   
                                MvcDynamicForms.Form form2 = UpDateSurveyModel(surveyInfoModel2, IsMobileDevice, FormValuesHasChanged, SurveyAnswer2,true);
                               
                                _isurveyFacade.UpdateSurveyResponse(surveyInfoModel2, Obj.ResponseId, form2, SurveyAnswer2, IsSubmited, true, PageNumber, UserId);
                                    }

                               if (!string.IsNullOrEmpty(CloseButton))
                                   {
                                   
                                   return RedirectToAction("Index", "Home", new { surveyid = this.RootFormId });
                                   }
                               else
                                   {
                                   if (!IsMobileDevice)
                                       {
                                       if (string.IsNullOrEmpty(this.RootFormId))
                                           {
                                           return RedirectToAction("Index", "Home", new { surveyid = surveyInfoModel.SurveyId });
                                           }
                                       else
                                           {
                                           return RedirectToAction("Index", "Home", new { surveyid = this.RootFormId });

                                           }
                                       }
                                   else
                                       {
                                       return RedirectToAction("Index", "FormResponse", new { formid = surveyInfoModel.SurveyId });

                                       }

                                   }
                                }
                            else
                                {
                                //This is a Navigation to a url

                                //////////////////////UpDate Survey Mode//////////////////////////
                                SurveyAnswer.IsDraftMode = surveyInfoModel.IsDraftMode;

                                form = SetLists(form);

                                _isurveyFacade.UpdateSurveyResponse(surveyInfoModel, responseId, form, SurveyAnswer, IsSubmited, IsSaved, PageNumber, UserId);

                                SurveyAnswer = _isurveyFacade.GetSurveyAnswerResponse(responseId).SurveyResponseList[0];
                                form = _isurveyFacade.GetSurveyFormData(surveyInfoModel.SurveyId, PageNumber, SurveyAnswer, IsMobileDevice, SurveyAnswerResponseList.SurveyResponseList);
                                form.FormValuesHasChanged = FormValuesHasChanged;
                                TempData["Width"] = form.Width + 5;
                                //PassCode start
                                if (IsMobileDevice)
                                    {
                                    form = SetFormPassCode(form, responseId);
                                    }
                                //passCode end
                                form.StatusId = SurveyAnswer.Status;
                                SurveyModel SurveyModel = new SurveyModel();
                                SurveyModel.Form = form;
                                SurveyModel.RelateModel = Mapper.ToRelateModel(FormsHierarchy, form.SurveyInfo.SurveyId);
                         
                                return View(Epi.Web.MVC.Constants.Constant.INDEX_PAGE, SurveyModel);
                                }

                            }
                        else {
                            //Invalid Data - stay on same page
                            int CurrentPageNum = GetSurveyPageNumber(SurveyAnswer.XML.ToString());



                            if (CurrentPageNum != PageNumber) // failed validation and navigating to different page// must keep url the same 
                                {
                                TempData["isredirect"] = "true";
                                TempData["Width"] = form.Width + 5;
                                return RedirectToAction("Index", "Survey", new { RequestId = form.ResponseId, PageNumber = CurrentPageNum });

                                }
                            else
                                {
                                TempData["Width"] = form.Width + 5;
                                SurveyModel SurveyModel = new SurveyModel();
                                SurveyModel.Form = form;
                                SurveyModel.RelateModel = Mapper.ToRelateModel(FormsHierarchy, form.SurveyInfo.SurveyId);
                              
                                return View(Epi.Web.MVC.Constants.Constant.INDEX_PAGE, SurveyModel);
                                }
                            }
                        
                    }

               
                }

            catch (Exception ex)
                {
                Epi.Web.Utility.ExceptionMessage.SendLogMessage(ex, this.HttpContext);

                return View(Epi.Web.MVC.Constants.Constant.EXCEPTION_PAGE);
                }

            }

        private int GetResponseCount(List<FormsHierarchyDTO> FormsHierarchy, int RequestedViewId, string responseId)
            {
            int ResponseCount = 0;
            var  ViewResponses = FormsHierarchy.Where(x => x.ViewId == RequestedViewId);

           foreach(var item in ViewResponses )
               
               {
               if (item.ResponseIds.Count>0)
                   {
                    var list = item.ResponseIds.Any(x => x.RelateParentId == responseId);
                    if (list == true)
                        {

                        ResponseCount++;
                        break;
                        }
                   }
               }

            return ResponseCount;
            }

        private List<FormsHierarchyDTO> GetFormsHierarchy()
            {
            FormsHierarchyResponse FormsHierarchyResponse = new FormsHierarchyResponse();
            FormsHierarchyRequest FormsHierarchyRequest = new FormsHierarchyRequest();
            if (Session["RootFormId"] != null && Session["RootResponseId"] != null)
                {
                FormsHierarchyRequest.SurveyInfo.FormId = Session["RootFormId"].ToString();
                FormsHierarchyRequest.SurveyResponseInfo.ResponseId = Session["RootResponseId"].ToString();
                FormsHierarchyResponse = _isurveyFacade.GetFormsHierarchy(FormsHierarchyRequest);
                }
            return FormsHierarchyResponse.FormsHierarchy;
            }


        private int GetCurrentPage()
            {
            int CurrentPage = 1;

            string PageNum = this.Request.UrlReferrer.ToString().Substring(this.Request.UrlReferrer.ToString().LastIndexOf('/') + 1);

            int.TryParse(PageNum, out CurrentPage);
            return CurrentPage;
            }




        private void SetCurrentPage(Epi.Web.Common.DTO.SurveyAnswerDTO surveyAnswerDTO, int PageNumber)
            {

            XDocument Xdoc = XDocument.Parse(surveyAnswerDTO.XML);
            if (PageNumber != 0)
                {
                Xdoc.Root.Attribute("LastPageVisited").Value = PageNumber.ToString();
                }

            surveyAnswerDTO.XML = Xdoc.ToString();

            Epi.Web.Common.Message.SurveyAnswerRequest sar = new Common.Message.SurveyAnswerRequest();
            sar.Action = "Update";
            sar.Criteria.UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            sar.SurveyAnswerList.Add(surveyAnswerDTO);

            this._isurveyFacade.GetSurveyAnswerRepository().SaveSurveyAnswer(sar);

            }



        private Epi.Web.Common.DTO.SurveyAnswerDTO GetSurveyAnswer(string responseId)
            {
            Epi.Web.Common.DTO.SurveyAnswerDTO result = null;

            //responseId = TempData[Epi.Web.MVC.Constants.Constant.RESPONSE_ID].ToString();
            result = _isurveyFacade.GetSurveyAnswerResponse(responseId).SurveyResponseList[0];

            return result;

            }



        private enum PreValidationResultEnum
            {
            Success,
            SurveyIsPastClosingDate,
            SurveyIsAlreadyCompleted
            }


        private PreValidationResultEnum PreValidateResponse(SurveyAnswerModel SurveyAnswer, SurveyInfoModel SurveyInfo)
            {
            PreValidationResultEnum result = PreValidationResultEnum.Success;

            if (DateTime.Now > SurveyInfo.ClosingDate)
                {
                return PreValidationResultEnum.SurveyIsPastClosingDate;
                }


            if (SurveyAnswer.Status == 3)
                {
                return PreValidationResultEnum.SurveyIsAlreadyCompleted;
                }

            return result;
            }

        private int GetSurveyPageNumber(string ResponseXml)
            {

            XDocument xdoc = XDocument.Parse(ResponseXml);

            int PageNumber = 0;

            if ((string)xdoc.Root.Attribute("LastPageVisited") != null)
                {
                PageNumber = int.Parse(xdoc.Root.Attribute("LastPageVisited").Value);
                }
            else
                {
                PageNumber = 1;
                }

            return PageNumber;

            }

        public static string GetResponseFormState(string Xml, string ListName)
            {

            string List = "";

            if (!string.IsNullOrEmpty(Xml))
                {
                XDocument xdoc = XDocument.Parse(Xml);

                if (!string.IsNullOrEmpty(xdoc.Root.Attribute(ListName).Value.ToString()))
                    {
                    List = xdoc.Root.Attribute(ListName).Value;


                    }

                }

            return List;
            }

        public static string GetRequiredList(string Xml)
            {
            XDocument Xdoc = XDocument.Parse(Xml);
            string list = Xdoc.Root.Attribute("RequiredFieldsList").Value;


            return list;
            }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateResponseXml(string NameList, string Value, string responseId)
            {
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            try
                {
                if (!string.IsNullOrEmpty(NameList))
                    {
                    string[] _NameList = null;


                    _NameList = NameList.Split(',');

                    bool IsMobileDevice = false;

                    IsMobileDevice = this.Request.Browser.IsMobileDevice;
                    Epi.Web.Common.DTO.SurveyAnswerDTO SurveyAnswer = _isurveyFacade.GetSurveyAnswerResponse(responseId).SurveyResponseList[0];

                    //  SurveyInfoModel surveyInfoModel = _isurveyFacade.GetSurveyInfoModel(SurveyAnswer.SurveyId);
                    SurveyInfoModel surveyInfoModel = GetSurveyInfo(SurveyAnswer.SurveyId);
                    int NumberOfPages = Epi.Web.MVC.Utility.SurveyHelper.GetNumberOfPags(SurveyAnswer.XML);

                    foreach (string Name in _NameList)
                        {
                        for (int i = NumberOfPages; i > 0; i--)
                            {
                            SurveyAnswer = _isurveyFacade.GetSurveyAnswerResponse(SurveyAnswer.ResponseId).SurveyResponseList[0];

                            MvcDynamicForms.Form formRs = _isurveyFacade.GetSurveyFormData(surveyInfoModel.SurveyId, i, SurveyAnswer, IsMobileDevice);

                            formRs = Epi.Web.MVC.Utility.SurveyHelper.UpdateControlsValues(formRs, Name, Value);

                            _isurveyFacade.UpdateSurveyResponse(surveyInfoModel, SurveyAnswer.ResponseId, formRs, SurveyAnswer, false, false, i, UserId);

                            }
                        }
                    return Json(true);
                    }
                return Json(true);
                }
            catch (Exception ex)
                {
                return Json(false);
                }
            }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult SaveSurvey(string Key, int Value, string responseId)
            {
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            try
                {
                bool IsMobileDevice = false;
                int PageNumber = Value;
                IsMobileDevice = this.Request.Browser.IsMobileDevice;


                Epi.Web.Common.DTO.SurveyAnswerDTO SurveyAnswer = _isurveyFacade.GetSurveyAnswerResponse(responseId).SurveyResponseList[0];



                //SurveyInfoModel surveyInfoModel = _isurveyFacade.GetSurveyInfoModel(SurveyAnswer.SurveyId);
                SurveyInfoModel surveyInfoModel = GetSurveyInfo(SurveyAnswer.SurveyId);
                PreValidationResultEnum ValidationTest = PreValidateResponse(Mapper.ToSurveyAnswerModel(SurveyAnswer), surveyInfoModel);
                var form = _isurveyFacade.GetSurveyFormData(SurveyAnswer.SurveyId, PageNumber, SurveyAnswer, IsMobileDevice);

                form.StatusId = SurveyAnswer.Status;
                var IsSaved = false;
                form.IsSaved = true;
                SurveyAnswer = _isurveyFacade.GetSurveyAnswerResponse(responseId).SurveyResponseList[0];
                form = _isurveyFacade.GetSurveyFormData(surveyInfoModel.SurveyId, GetSurveyPageNumber(SurveyAnswer.XML.ToString()) == 0 ? 1 : GetSurveyPageNumber(SurveyAnswer.XML.ToString()), SurveyAnswer, IsMobileDevice);
                //Update the model
                UpdateModel(form);
                //Save the child form
                _isurveyFacade.UpdateSurveyResponse(surveyInfoModel, responseId, form, SurveyAnswer, false, IsSaved, PageNumber, UserId);
               //  SetCurrentPage(SurveyAnswer, PageNumber);
                //Save the parent form 
                IsSaved = form.IsSaved = true;
                _isurveyFacade.UpdateSurveyResponse(surveyInfoModel, responseId, form, SurveyAnswer, false, IsSaved, PageNumber, UserId);
                return Json(true);

                }
            catch (Exception ex)
                {
                return Json(false);
                }
            }


        //[OutputCache(Duration = int.MaxValue, VaryByParam = "SurveyId", Location = OutputCacheLocation.Server)]
        public SurveyInfoModel GetSurveyInfo(string SurveyId)
            {

            /* var CacheObj = HttpRuntime.Cache.Get(SurveyId);
             if (CacheObj ==null)
             {

                        SurveyInfoModel surveyInfoModel = _isurveyFacade.GetSurveyInfoModel(SurveyId);
                        HttpRuntime.Cache.Insert(SurveyId, surveyInfoModel, null, Cache.NoAbsoluteExpiration, TimeSpan.FromDays(1));
             
                     return surveyInfoModel;
                }
                else
              
                {
                    return (SurveyInfoModel)CacheObj;
      
                }*/
            SurveyInfoModel surveyInfoModel = _isurveyFacade.GetSurveyInfoModel(SurveyId);
            return surveyInfoModel;

            }
        public MvcDynamicForms.Form SetLists(MvcDynamicForms.Form form)
            {

            form.HiddenFieldsList = this.Request.Form["HiddenFieldsList"].ToString();

            form.HighlightedFieldsList = this.Request.Form["HighlightedFieldsList"].ToString();

            form.DisabledFieldsList = this.Request.Form["DisabledFieldsList"].ToString();

            form.RequiredFieldsList = this.Request.Form["RequiredFieldsList"].ToString();

            form.AssignList = this.Request.Form["AssignList"].ToString();

            return form;
            }

        [HttpPost]

        public ActionResult Delete(string ResponseId)//List<FormInfoModel> ModelList, string formid)
            {
            
            SurveyAnswerRequest SARequest = new SurveyAnswerRequest();
            SARequest.SurveyAnswerList.Add(new SurveyAnswerDTO() { ResponseId = Session["RootResponseId"].ToString() });
            SARequest.Criteria.UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            SurveyAnswerResponse SAResponse = _isurveyFacade.DeleteResponse(SARequest);

            return Json(Session["RootFormId"]);//string.Empty
            //return RedirectToAction("Index", "Home");
            }
        [HttpPost]

        public ActionResult DeleteBranch(string ResponseId)//List<FormInfoModel> ModelList, string formid)
            {
            
            SurveyAnswerRequest SARequest = new SurveyAnswerRequest();
            SARequest.SurveyAnswerList.Add(new SurveyAnswerDTO() { ResponseId = ResponseId });
            SARequest.Criteria.UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            SurveyAnswerResponse SAResponse = _isurveyFacade.DeleteResponse(SARequest);

            return Json(Session["RootFormId"]);//string.Empty
            //return RedirectToAction("Index", "Home");
            }
        [HttpGet]
        public ActionResult LogOut()
            {

            FormsAuthentication.SignOut();
            return RedirectToAction("Index", "Login");


            }

        [HttpPost]
        public JsonResult AddChild( string SurveyId, int ViewId, string ResponseId, string FormValuesHasChanged, string CurrentPage)
            {
          
           
            string ChildResponseId = AddNewChild(SurveyId, ViewId, ResponseId, FormValuesHasChanged, CurrentPage);
           
            return Json(ChildResponseId);

            }
        private string AddNewChild( string SurveyId, int ViewId, string ResponseId, string FormValuesHasChanged, string CurrentPage ) 
            { 
            
             bool IsMobileDevice = this.Request.Browser.IsMobileDevice;
            if (IsMobileDevice == false)
                {
                IsMobileDevice = Epi.Web.MVC.Utility.SurveyHelper.IsMobileDevice(this.Request.UserAgent.ToString());
                }
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            //1-Get the child Id
            //SurveyInfoResponse GetChildFormInfo(SurveyInfoRequest SurveyInfoRequest)
            SurveyInfoRequest SurveyInfoRequest = new Common.Message.SurveyInfoRequest();
            SurveyInfoResponse SurveyInfoResponse = new Common.Message.SurveyInfoResponse();
            SurveyInfoDTO SurveyInfoDTO = new Common.DTO.SurveyInfoDTO();
            SurveyInfoDTO.SurveyId = SurveyId;
            SurveyInfoDTO.ViewId = ViewId;
            SurveyInfoRequest.SurveyInfoList.Add(SurveyInfoDTO);
            //SurveyInfoResponse = _isurveyFacade.GetChildFormInfo(SurveyInfoRequest);
            


            //3-Create a new response for the child 
            //string ChildResponseId = CreateResponse(SurveyInfoResponse.SurveyInfoList[0].SurveyId, ResponseId);
            string ChildResponseId = CreateResponse(SurveyId, ResponseId);

            //Session["RelateButtonWasClicked"] = "true";
            //4-returen response id to client

            return ChildResponseId;
            }
        [HttpPost]
        public JsonResult HasResponse(string SurveyId, int ViewId, string ResponseId)
            {
            List<FormsHierarchyDTO> FormsHierarchy = GetFormsHierarchy();
            bool HasResponse  = false;
            bool IsMobileDevice = this.Request.Browser.IsMobileDevice;
            if (IsMobileDevice == false)
                {
                IsMobileDevice = Epi.Web.MVC.Utility.SurveyHelper.IsMobileDevice(this.Request.UserAgent.ToString());
                }
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            //1-Get the child Id
            //SurveyInfoResponse GetChildFormInfo(SurveyInfoRequest SurveyInfoRequest)
            SurveyInfoRequest SurveyInfoRequest = new Common.Message.SurveyInfoRequest();
            SurveyInfoResponse SurveyInfoResponse = new Common.Message.SurveyInfoResponse();
            SurveyInfoDTO SurveyInfoDTO = new Common.DTO.SurveyInfoDTO();
            SurveyInfoDTO.SurveyId = SurveyId;
            SurveyInfoDTO.ViewId = ViewId;
            SurveyInfoRequest.SurveyInfoList.Add(SurveyInfoDTO);
            SurveyInfoResponse = _isurveyFacade.GetChildFormInfo(SurveyInfoRequest);
            var RelateSurveyId = FormsHierarchy.Single(x => x.ViewId == ViewId);

            int ResponseCount = GetResponseCount(FormsHierarchy, ViewId, ResponseId);

                if (ResponseCount > 0)
                {
                               
            
                HasResponse = true ;
                }
           

            
            return Json(HasResponse);

            }
        public string CreateResponse(string SurveyId, string RelateResponseId)
            {
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            bool.TryParse(Session["IsEditMode"].ToString(), out this.IsEditMode);
            //if (!string.IsNullOrEmpty(EditForm))
            //    {
            //    Epi.Web.Common.DTO.SurveyAnswerDTO surveyAnswerDTO = GetSurveyAnswer(EditForm);
            //    string ChildRecordId = GetChildRecordId(surveyAnswerDTO);

            //    }
            bool IsMobileDevice = this.Request.Browser.IsMobileDevice;


            if (IsMobileDevice == false)
                {
                IsMobileDevice = Epi.Web.MVC.Utility.SurveyHelper.IsMobileDevice(this.Request.UserAgent.ToString());
                }
            //create the responseid
            Guid ResponseID = Guid.NewGuid();
            TempData[Epi.Web.MVC.Constants.Constant.RESPONSE_ID] = ResponseID.ToString();

            // create the first survey response
            // Epi.Web.Common.DTO.SurveyAnswerDTO SurveyAnswer = _isurveyFacade.CreateSurveyAnswer(surveyModel.SurveyId, ResponseID.ToString());
           
            Epi.Web.Common.DTO.SurveyAnswerDTO SurveyAnswer = _isurveyFacade.CreateSurveyAnswer(SurveyId, ResponseID.ToString(), UserId, true, RelateResponseId,this.IsEditMode);
            SurveyInfoModel surveyInfoModel = GetSurveyInfo(SurveyAnswer.SurveyId);

            // set the survey answer to be production or test 
            SurveyAnswer.IsDraftMode = surveyInfoModel.IsDraftMode;
            XDocument xdoc = XDocument.Parse(surveyInfoModel.XML);

            MvcDynamicForms.Form form = _isurveyFacade.GetSurveyFormData(SurveyAnswer.SurveyId, 1, SurveyAnswer, IsMobileDevice);

            var _FieldsTypeIDs = from _FieldTypeID in
                                     xdoc.Descendants("Field")
                                 select _FieldTypeID;

            TempData["Width"] = form.Width + 100;

            XDocument xdocResponse = XDocument.Parse(SurveyAnswer.XML);

            XElement ViewElement = xdoc.XPathSelectElement("Template/Project/View");
            string checkcode = ViewElement.Attribute("CheckCode").Value.ToString();

            form.FormCheckCodeObj = form.GetCheckCodeObj(xdoc, xdocResponse, checkcode);

            ///////////////////////////// Execute - Record Before - start//////////////////////
            Dictionary<string, string> ContextDetailList = new Dictionary<string, string>();
            EnterRule FunctionObject_B = (EnterRule)form.FormCheckCodeObj.GetCommand("level=record&event=before&identifier=");
            SurveyResponseXML SurveyResponseXML = new SurveyResponseXML(PageFields, RequiredList);
            if (FunctionObject_B != null && !FunctionObject_B.IsNull())
                {
                try
                    {

                    SurveyAnswer.XML = SurveyResponseXML.CreateResponseDocument(xdoc, SurveyAnswer.XML);
                    //SurveyAnswer.XML = Epi.Web.MVC.Utility.SurveyHelper.CreateResponseDocument(xdoc, SurveyAnswer.XML, RequiredList);
                    Session["RequiredList"] = SurveyResponseXML._RequiredList;
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

            SurveyAnswer = _isurveyFacade.GetSurveyAnswerResponse(SurveyAnswer.ResponseId).SurveyResponseList[0];




            return ResponseID.ToString();


            }


        private MvcDynamicForms.Form SetFormPassCode(MvcDynamicForms.Form form, string responseId)
            {

            Epi.Web.Common.Message.UserAuthenticationResponse AuthenticationResponse = _isurveyFacade.GetAuthenticationResponse(responseId);

            string strPassCode = Epi.Web.MVC.Utility.SurveyHelper.GetPassCode();
            if (string.IsNullOrEmpty(AuthenticationResponse.PassCode))
                {
                _isurveyFacade.UpdatePassCode(responseId, strPassCode);
                }
            if (AuthenticationResponse.PassCode == null)
                {
                form.PassCode = strPassCode;

                }
            else
                {
                form.PassCode = AuthenticationResponse.PassCode;
                }
            

            return form;
            }

        private MvcDynamicForms.Form UpDateSurveyModel(SurveyInfoModel surveyInfoModel, bool IsMobileDevice, string FormValuesHasChanged, SurveyAnswerDTO SurveyAnswer, bool IsSaveAndClose = false )
            {
            MvcDynamicForms.Form form = new MvcDynamicForms.Form();
            int CurrentPageNum = GetSurveyPageNumber(SurveyAnswer.XML.ToString());


            string url = "";
            if (this.Request.UrlReferrer == null)
                {
                url = this.Request.Url.ToString();
                }
            else
                {
                url = this.Request.UrlReferrer.ToString();
                }
            //  url = this.Request.Url.ToString();
            int LastIndex = url.LastIndexOf("/");
            string StringNumber = null;
            if (url.Length - LastIndex + 1 <= url.Length)
                {
                StringNumber = url.Substring(LastIndex, url.Length - LastIndex);
                StringNumber = StringNumber.Trim('/');
                }

            if (int.TryParse(StringNumber, out ReffererPageNum))
                {
                if (ReffererPageNum != CurrentPageNum)
                    {
                    form = _isurveyFacade.GetSurveyFormData(surveyInfoModel.SurveyId, ReffererPageNum, SurveyAnswer, IsMobileDevice);
                    form.FormValuesHasChanged = FormValuesHasChanged;
                    if (IsMobileDevice)
                        {
                        Epi.Web.MVC.Utility.MobileFormProvider.UpdateHiddenFields(ReffererPageNum, form, XDocument.Parse(surveyInfoModel.XML), XDocument.Parse(SurveyAnswer.XML), this.ControllerContext.RequestContext.HttpContext.Request.Form);
                        }
                    else
                        {
                        Epi.Web.MVC.Utility.FormProvider.UpdateHiddenFields(ReffererPageNum, form, XDocument.Parse(surveyInfoModel.XML), XDocument.Parse(SurveyAnswer.XML), this.ControllerContext.RequestContext.HttpContext.Request.Form);
                        }
                    }
                else
                    {
                    form = _isurveyFacade.GetSurveyFormData(surveyInfoModel.SurveyId, CurrentPageNum, SurveyAnswer, IsMobileDevice);
                    form.FormValuesHasChanged = FormValuesHasChanged;
                    if (IsMobileDevice)
                        {
                        Epi.Web.MVC.Utility.MobileFormProvider.UpdateHiddenFields(CurrentPageNum, form, XDocument.Parse(surveyInfoModel.XML), XDocument.Parse(SurveyAnswer.XML), this.ControllerContext.RequestContext.HttpContext.Request.Form);
                        }
                    else
                        {
                        Epi.Web.MVC.Utility.FormProvider.UpdateHiddenFields(CurrentPageNum, form, XDocument.Parse(surveyInfoModel.XML), XDocument.Parse(SurveyAnswer.XML), this.ControllerContext.RequestContext.HttpContext.Request.Form);
                        }
                    }


                if (!IsSaveAndClose)
                    {
                    UpdateModel(form);
                    }
                }
            else
                {
                //get the survey form
                form = _isurveyFacade.GetSurveyFormData(surveyInfoModel.SurveyId, GetSurveyPageNumber(SurveyAnswer.XML.ToString()), SurveyAnswer, IsMobileDevice);
                form.FormValuesHasChanged = FormValuesHasChanged;
                form.ClearAllErrors();
                if (ReffererPageNum == 0)
                    {
                    int index = 1;
                    if (StringNumber.Contains("?RequestId="))
                        {
                        index = StringNumber.IndexOf("?");
                        }

                    ReffererPageNum = int.Parse(StringNumber.Substring(0, index));

                    }
                if (ReffererPageNum == CurrentPageNum)
                    {
                    UpdateModel(form);
                    }
                UpdateModel(form);
                }
            return form;
            }
        private void ExecuteRecordAfterCheckCode(MvcDynamicForms.Form form, SurveyInfoModel surveyInfoModel,SurveyAnswerDTO SurveyAnswer,string responseId,int PageNumber,int UserId)
            {

        EnterRule FunctionObject_A = (EnterRule)form.FormCheckCodeObj.GetCommand("level=record&event=after&identifier=");
        if (FunctionObject_A != null && !FunctionObject_A.IsNull())
            {
            try
                {
                FunctionObject_A.Execute();
                }
            catch (Exception ex)
                {
                // do nothing so that processing can 
                // continue
                }
            }
        Dictionary<string, string> ContextDetailList = new Dictionary<string, string>();
        ContextDetailList = Epi.Web.MVC.Utility.SurveyHelper.GetContextDetailList(FunctionObject_A);
        form = Epi.Web.MVC.Utility.SurveyHelper.UpdateControlsValuesFromContext(form, ContextDetailList);
        _isurveyFacade.UpdateSurveyResponse(surveyInfoModel, responseId, form, SurveyAnswer, false, false, PageNumber, UserId);
            
            
            }

        private KeyValuePair<string, int> ValidateAll(MvcDynamicForms.Form form, int UserId, bool IsSubmited, bool IsSaved, bool IsMobileDevice, string FormValuesHasChanged)
            {
              List<FormsHierarchyDTO> FormsHierarchy = GetFormsHierarchy();
              KeyValuePair<string, int> result = new KeyValuePair<string,int>();
             // foreach (var FormObj in FormsHierarchy)
              for (int j = FormsHierarchy.Count() - 1; j >= 0; --j)
                  {
                  foreach (var Obj in FormsHierarchy[j].ResponseIds)
                          {
                          SurveyAnswerDTO SurveyAnswer = _isurveyFacade.GetSurveyAnswerResponse(Obj.ResponseId).SurveyResponseList[0];
                          
                          SurveyInfoModel surveyInfoModel = GetSurveyInfo(SurveyAnswer.SurveyId);
                          SurveyAnswer.IsDraftMode = surveyInfoModel.IsDraftMode;
                          form = UpDateSurveyModel(surveyInfoModel, IsMobileDevice, FormValuesHasChanged, SurveyAnswer);

                          for (int i = 1; i < form.NumberOfPages + 1; i++)
                              {

                              form = Epi.Web.MVC.Utility.FormProvider.GetForm(form.SurveyInfo, i, SurveyAnswer);
                              if (!form.Validate(form.RequiredFieldsList))
                                  {
                                  TempData["isredirect"] = "true";
                                  TempData["Width"] = form.Width + 5;
                                  //  return View(Epi.Web.MVC.Constants.Constant.INDEX_PAGE, form);
                                  _isurveyFacade.UpdateSurveyResponse(surveyInfoModel, Obj.ResponseId, form, SurveyAnswer, IsSubmited, IsSaved, i, UserId);

                                  result = new KeyValuePair<string, int>(Obj.ResponseId, i);
                                  goto Exit;
                                  }
                              //else
                              //    {
                              //    //ExecuteRecordAfterCheckCode(form, surveyInfoModel, SurveyAnswer, responseId, i, UserId);
                                  
                              //    }
                              // create my list of objects 
                              
                              }
                          ListSurveyAnswerDTO.Add(SurveyAnswer);
                          }
                  }
                   Exit:
                  return result;
            
            }
        private MvcDynamicForms.Form SaveCurrentForm(MvcDynamicForms.Form form, SurveyInfoModel surveyInfoModel, SurveyAnswerDTO SurveyAnswer, string responseId, int UserId, bool IsSubmited, bool IsSaved, bool IsMobileDevice, string FormValuesHasChanged, int PageNumber)
            {



        SurveyAnswer = _isurveyFacade.GetSurveyAnswerResponse(responseId).SurveyResponseList[0];
        
        SurveyAnswer.IsDraftMode = surveyInfoModel.IsDraftMode;

        form = _isurveyFacade.GetSurveyFormData(surveyInfoModel.SurveyId, GetSurveyPageNumber(SurveyAnswer.XML.ToString()) == 0 ? 1 : GetSurveyPageNumber(SurveyAnswer.XML.ToString()), SurveyAnswer, IsMobileDevice);
        form.FormValuesHasChanged = FormValuesHasChanged;

        UpdateModel(form);
        
        form.IsSaved = true;
        form.StatusId = SurveyAnswer.Status;

        // Pass Code Logic  start 
        form = SetFormPassCode(form, responseId);
        // Pass Code Logic  end 
        _isurveyFacade.UpdateSurveyResponse(surveyInfoModel, responseId, form, SurveyAnswer, IsSubmited, IsSaved, PageNumber, UserId);

        return form;  
            
            }
        private void SetGlobalVariable()
            {

            if (Session["RootFormId"] != null  )
                {
                this.RootFormId = Session["RootFormId"].ToString();
                
                }
            if (  Session["RootResponseId"] != null)
                {
                
                this.RootResponseId = Session["RootResponseId"].ToString();
                }
            if (Session["RequiredList"] != null)
                {
                this.RequiredList = Session["RequiredList"].ToString();
                }
           
                bool.TryParse(Session["IsEditMode"].ToString(), out this.IsEditMode);
            
            }
        private FormResponseInfoModel GetFormResponseInfoModel(string SurveyId, string  ResponseId)
            {
            int UserId = SurveyHelper.GetDecryptUserId(Session["UserId"].ToString());
            FormResponseInfoModel FormResponseInfoModel = new FormResponseInfoModel();
            
            SurveyResponseXML SurveyResponseXML = new SurveyResponseXML();
            if (!string.IsNullOrEmpty(SurveyId))
                {
                SurveyAnswerRequest FormResponseReq = new SurveyAnswerRequest();
                FormSettingRequest FormSettingReq = new Common.Message.FormSettingRequest();

                //Populating the request

                FormSettingReq.FormInfo.FormId = SurveyId;
                FormSettingReq.FormInfo.UserId = UserId;
                //Getting Column Name  List
                FormSettingResponse FormSettingResponse = _isurveyFacade.GetFormSettings(FormSettingReq);
                Columns = FormSettingResponse.FormSetting.ColumnNameList.ToList();
                Columns.Sort(Compare);

                // Setting  Column Name  List
                FormResponseInfoModel.Columns = Columns;

                //Getting Resposes
                FormResponseReq.Criteria.SurveyId = SurveyId.ToString();
                FormResponseReq.Criteria.SurveyAnswerIdList.Add(ResponseId);
                 
                FormResponseReq.Criteria.PageNumber = 1;
                FormResponseReq.Criteria.UserId = UserId;
                SurveyAnswerResponse FormResponseList = _isurveyFacade.GetResponsesByRelatedFormId(FormResponseReq);
                //Setting Resposes List
                List<ResponseModel> ResponseList = new List<ResponseModel>();
                foreach (var item in FormResponseList.SurveyResponseList)
                    {
                    ResponseList.Add(SurveyResponseXML.ConvertXMLToModel(item, Columns));
                    }

                FormResponseInfoModel.ResponsesList = ResponseList;
                //Setting Form Info 
              //  FormResponseInfoModel.FormInfoModel = Mapper.ToFormInfoModel(FormResponseList.FormInfo);
                //Setting Additional Data

                FormResponseInfoModel.NumberOfPages = FormResponseList.NumberOfPages;
                FormResponseInfoModel.PageSize = ReadPageSize();
                FormResponseInfoModel.NumberOfResponses = FormResponseList.NumberOfResponses;
                FormResponseInfoModel.CurrentPage = 1;
                }
            return FormResponseInfoModel;
            }

        private int Compare(KeyValuePair<int, string> a, KeyValuePair<int, string> b)
            {
            return a.Key.CompareTo(b.Key);
            }
        private int ReadPageSize()
            {
            return Convert.ToInt16(WebConfigurationManager.AppSettings["RESPONSE_PAGE_SIZE"].ToString());
            }


       
        [HttpGet]
       
        public ActionResult ReadResponseInfo(string SurveyId, int ViewId, string ResponseId)//List<FormInfoModel> ModelList, string formid)
            {
            bool IsMobileDevice = this.Request.Browser.IsMobileDevice;

          //  var model = new FormResponseInfoModel();
            List<FormsHierarchyDTO> FormsHierarchy = GetFormsHierarchy();
            int RequestedViewId;
            RequestedViewId = ViewId;
            //form = SaveCurrentForm(form, surveyInfoModel, SurveyAnswer, responseId, UserId, IsSubmited, IsSaved, IsMobileDevice, FormValuesHasChanged, PageNumber);
            //form = SetLists(form);
            //TempData["Width"] = form.Width + 5;
            Session["RequestedViewId"] = RequestedViewId;
            SurveyModel SurveyModel = new SurveyModel();

            SurveyModel.RequestedViewId = 10;
            //  SurveyModel.Form = OldSurveyModel.Form;
            SurveyModel.RelateModel = Mapper.ToRelateModel(FormsHierarchy, SurveyId);
            SurveyModel.RequestedViewId = RequestedViewId;

            var RelateSurveyId = FormsHierarchy.Single(x => x.ViewId == ViewId);
            SurveyModel.FormResponseInfoModel = GetFormResponseInfoModel(RelateSurveyId.FormId, ResponseId);
        

          //  model = GetFormResponseInfoModel(formid, page);

            if (IsMobileDevice == false)
                {
                return PartialView("ListResponses", SurveyModel);
                }
            else
                {
                return View("ListResponses", SurveyModel);
                }
            }
        
        }
    }





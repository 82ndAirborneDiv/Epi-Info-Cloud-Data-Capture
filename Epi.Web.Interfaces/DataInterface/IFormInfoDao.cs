﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Epi.Web.Enter.Common.BusinessObject;

namespace Epi.Web.Enter.Interfaces.DataInterface
    {
    public interface IFormInfoDao
        {
         List<FormInfoBO> GetFormInfo(int UserId , int CurrentOrgId);
         FormInfoBO GetFormByFormId(string FormId, bool GetXml, int UserId);
         FormInfoBO GetFormByFormId(string FormId);
         bool GetEwavLiteToggleSwitch(string FormId, int UserId);


         bool HasDraftRecords(string FormId);
        }
    }

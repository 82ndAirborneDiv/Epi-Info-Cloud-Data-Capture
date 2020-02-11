﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Epi.Web.Enter.Common.DTO;
using System.Runtime.Serialization;

namespace Epi.Web.Enter.Common.Message
    {
     [DataContract(Namespace = "http://www.yourcompany.com/types/")]
    public class OrganizationAccountResponse : Epi.Web.Enter.Common.MessageBase.ResponseBase 
        {
         public OrganizationAccountResponse() { }

  
         [DataMember]
         public string AdminMessage;
         [DataMember]
         public string OrgMessage;
         /// <summary>
         /// OrganizationInfo object.
         /// </summary>
         [DataMember]
         public  OrganizationDTO  OrganizationDTO;
         /// <summary>
         /// Admin object.
         /// </summary>
         [DataMember]
         public  AdminDTO  AdminList;
         [DataMember]
         public List<StateDTO> StateList;

        }
    }

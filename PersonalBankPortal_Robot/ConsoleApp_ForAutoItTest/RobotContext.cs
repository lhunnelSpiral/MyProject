﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp_ForAutoItTest
{
    public class RobotContext
    {
        public string LoginPassword { get; set; }
        public string ToAccountNumber { get; set; }
        public string ToAccountName { get; set; }
        public string ToBankName { get; set; }
        public string WithdrawAmount { get; set; }
        public string WithdrawTransactionId { get; set; }
        public string TokenWithdrawPin { get; set; }
    }

}

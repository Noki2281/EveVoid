﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EveVoid.Models.Shared
{
    public interface IEsiEntity
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}

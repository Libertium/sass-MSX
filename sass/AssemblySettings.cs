﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace sasSX
{
    public class AssemblySettings
    {
        public AssemblySettings()
        {
            // Some values intentionally redundantly set for clarity
			Verbose = VerboseLevels.Quiet;
            ListingOutput = null;
            IncludePath = new string[0];
            SymbolOutput = null;
            Encoding = Encoding.UTF8;
            AllowNestedMacros = true;
        }

		public VerboseLevels Verbose { get; set; }
        public string ListingOutput { get; set; }
        public string[] IncludePath { get; set; }
        public string SymbolOutput { get; set; }
        public Encoding Encoding { get; set; }
        public bool AllowNestedMacros { get; set; }
    }
}

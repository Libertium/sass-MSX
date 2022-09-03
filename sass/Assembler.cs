using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace sasSX
{
    public class Assembler
    {
        public static bool AllowExec = true;

        public InstructionSet InstructionSet { get; set; }
        public ExpressionEngine ExpressionEngine { get; set; }
        public AssemblyOutput Output { get; set; }
        public Encoding Encoding { get; set; }
        public List<string> IncludePaths { get; set; }
        public List<Macro> Macros { get; set; }
        public AssemblySettings Settings { get; set; }

        private uint PC { get; set; }
        private string[] Lines { get; set; }
        private int RootLineNumber { get; set; }
        private Stack<int> LineNumbers { get; set; }
        private Stack<string> FileNames { get; set; }
        private Stack<bool> IfStack { get; set; }
        private Stack<bool> WorkingIfStack { get; set; }
        /// <summary>
        /// Gets or sets the suspended lines.
        /// </summary>
        /// <value>The suspended lines.</value>
		private int SuspendedLines { get; set; }
        private int CurrentIndex { get; set; }
        private string CurrentLine { get; set; }
        private bool Listing { get; set; }

		private Stack<string> moduleNamespace;

		/// <summary>
		/// Cesc: Gets or sets a value indicating whether this <see cref="sass.Assembler"/> is ASMS.
		/// </summary>
		/// <value><c>true</c> if ASMS; otherwise, <c>false</c>.</value>
		public bool ASMSX { get; set; }
		public bool IsROM = false;
		public bool IsMegaROM = false;
		public int MegaROMPageSize;
		public List<ORGItem> ORGsList { get; set; }
		public uint ROMStart;
		string ROMStartLabel ="";
		uint currentSubpageORG;
		uint PCRepeat { get; set; }
		ulong repeatAmount;
		uint phasePC { get; set; }
		uint dephasePC { get; set; }

		public struct ORGItem
		{
			public uint ORGAdress;
			public uint ORGLength;
			public uint Subpage;

			public ORGItem(uint adress, uint endAdress, uint subpage)
			{
				ORGAdress = adress;
				ORGLength = endAdress;
				Subpage = subpage;
			}
		}

        public Assembler(InstructionSet instructionSet, AssemblySettings settings)
        {
            InstructionSet = instructionSet;
            Settings = settings;
            ExpressionEngine = new ExpressionEngine(settings);
            SuspendedLines = 0;
            LineNumbers = new Stack<int>();
            FileNames = new Stack<string>();
            IncludePaths = new List<string>();
            Macros = new List<Macro>();
            IfStack = new Stack<bool>();
            WorkingIfStack = new Stack<bool>();
            Listing = true;

			ORGsList = new List<ORGItem> (new ORGItem[1]);
			moduleNamespace = new Stack<string>();
        }

        readonly string[] ifDirectives = new[] { "endif", "else", "elif", "elseif", "ifdef", "ifndef", "if" };

        public AssemblyOutput Assemble(string assembly, string fileName = null)
        {
            Output = new AssemblyOutput();
            Output.InstructionSet = InstructionSet;
			bool blockComent = false;

			// Cesc
			//assembly = assembly.Replace("\r", "");
			assembly = assembly.Replace("\r", "").Replace('\t',' ');
			if (ASMSX)
				assembly = assembly.Replace ("@@", ".");

            PC = 0;
            Lines = assembly.Split('\n');
            FileNames.Push(Path.GetFileName(fileName));
            LineNumbers.Push(0);
            RootLineNumber = 0;
            IfStack.Push(true);
            for (CurrentIndex = 0; CurrentIndex < Lines.Length; CurrentIndex++)
            {
				CurrentLine = Lines [CurrentIndex].Trim ().TrimComments ();					

				/// Cesc for ASMSX, remove block coments /* */ and //
				if (ASMSX) {
					if (blockComent) {
						if (Lines [CurrentIndex].IndexOf ("*/") != -1) {
							if (Settings.Verbose == VerboseLevels.Diagnostic)
								Console.WriteLine ("{0} End block comment: {1} {2}", FileNames.Peek (), LineNumbers.Peek (), RootLineNumber);
						
							CurrentLine = Lines [CurrentIndex].Substring (
								Lines [CurrentIndex].IndexOf ("*/") + 2).Trim ().TrimComments ();
							blockComent = false;
						} else {
							CurrentLine = "";
						}
					}
					if (!blockComent) {
						if (Lines [CurrentIndex].IndexOf ("/*") != -1) {
							blockComent = true;
							if (Settings.Verbose == VerboseLevels.Diagnostic)
								Console.WriteLine ("{0} Begind block comment: {1} {2}", FileNames.Peek (), LineNumbers.Peek (), RootLineNumber);
							
							CurrentLine = Lines [CurrentIndex].Substring (
								0, Lines [CurrentIndex].IndexOf ("/*")).Trim ().TrimComments ();						
						} 
					}
					// remove single line coments: "//"
					if (CurrentLine.IndexOf ("//") != -1)
						CurrentLine = CurrentLine.Substring (0, CurrentLine.IndexOf ("//"));
				}

                if (SuspendedLines == 0)
                {
                    LineNumbers.Push(LineNumbers.Pop() + 1);
                    RootLineNumber++;
                }
                else
                    SuspendedLines--;

                if (!IfStack.Peek())
                {
                    bool match = false;
                    if (CurrentLine.StartsWith("#") || CurrentLine.StartsWith("."))
                    {
                        var directive = CurrentLine.Substring(1);
                        if (CurrentLine.Contains((' ')))
                            directive = directive.Remove(CurrentLine.IndexOf((' '))).Trim();
                        if (ifDirectives.Contains(directive.ToLower()))
                            match = true;
                    }
                    if (!match)
                        continue;
                }

                if (CurrentLine.SafeContains(".equ") && !CurrentLine.StartsWith(".equ"))
                {
                    var name = CurrentLine.Remove(CurrentLine.SafeIndexOf(".equ"));
                    var definition = CurrentLine.Substring(CurrentLine.SafeIndexOf(".equ") + 4);
                    CurrentLine = ".equ " + name.Trim() + " " + definition.Trim();
                }
					

				// Cesc: fer que accepti els db de la mateixa forma que .db
				if (ASMSX && CurrentLine.Trim ().StartsWith ("db ")) {
					CurrentLine = "." + CurrentLine;
					//Console.WriteLine (CurrentLine);
				}

				// Cesc: fer que accepti els dw de la mateixa forma que .dw
				if (ASMSX && CurrentLine.Trim ().StartsWith ("dw ")) {
					CurrentLine = "." + CurrentLine;
					//Console.WriteLine (CurrentLine);
				}
					
                // Check for macro
                if (!CurrentLine.StartsWith(".macro") && !CurrentLine.StartsWith("#macro") && !CurrentLine.StartsWith(".undefine") && !CurrentLine.StartsWith("#undefine"))
                {
                    Macro macroMatch = null;
                    string[] parameters = null;
                    string parameterDefinition = null;
                    foreach (var macro in Macros)
                    {
                        if (CurrentLine.ToLower().SafeContains(macro.Name))
                        {
                            // Try to match
                            int startIndex = CurrentLine.ToLower().SafeIndexOf(macro.Name);
                            int endIndex = startIndex + macro.Name.Length - 1;
                            if (macro.Parameters.Length != 0)
                            {
                                if (endIndex + 1 >= CurrentLine.Length)
                                    continue;
                                if (CurrentLine.Length < endIndex + 1 || CurrentLine[endIndex + 1] != '(')
                                    continue;
                                if (macroMatch != null && macro.Name.Length < macroMatch.Name.Length)
                                    continue;
                                parameterDefinition = CurrentLine.Substring(endIndex + 2, CurrentLine.LastIndexOf(')') - (endIndex + 2));
                                parameters = parameterDefinition.SafeSplit(',');
                                if (parameters.Length != macro.Parameters.Length)
                                    continue;
                                // Matched
                                macroMatch = macro;
                            }
                            else
                                macroMatch = macro;
                        }
                    }
                    if (macroMatch != null)
                    {
                        // Add an entry to the listing
                        AddOutput(CodeType.Directive);
                        var code = macroMatch.Code;
                        int index = 0;
                        foreach (var parameter in macroMatch.Parameters)
                            code = code.Replace(parameter.Trim(), parameters[index++].Trim());
                        string newLine;
                        if (parameterDefinition != null)
                            newLine = CurrentLine.Replace(macroMatch.Name + "(" + parameterDefinition + ")", code, StringComparison.InvariantCultureIgnoreCase);
                        else
                        {
                            if (CurrentLine.Substring(CurrentLine.ToLower().IndexOf(macroMatch.Name) + macroMatch.Name.Length).StartsWith("()"))
                                newLine = CurrentLine.Replace(macroMatch.Name + "()", code, StringComparison.InvariantCultureIgnoreCase);
                            else
                                newLine = CurrentLine.Replace(macroMatch.Name, code, StringComparison.InvariantCultureIgnoreCase);
                        }
                        var newLines = newLine.Replace("\r\n", "\n").Split('\n');
                        SuspendedLines += newLines.Length;
                        // Insert macro
                        Lines = Lines.Take(CurrentIndex).Concat(newLines).Concat(Lines.Skip(CurrentIndex + 1)).ToArray();
                        CurrentIndex--;
                        continue;
                    }
                }

                // Find same-line labels
                if (CurrentLine.Contains(":"))
                {
                    int length = 0;
                    bool isLabel = true;
                    for (int j = 0; j < CurrentLine.Length; j++)
                    {
                        if (char.IsLetterOrDigit(CurrentLine[j]) || CurrentLine[j] == '_')
                            length++;
                        else if (CurrentLine[j] == ':')
                            break;
                        else
                        {
                            isLabel = false;
                            break;
                        }
                    }
                    if (isLabel)
                    {
                        var label = CurrentLine.Remove(length).ToLower();
                        label = label.ToLower();
                        if (label == "_")
                        {
                            // Relative
                            ExpressionEngine.RelativeLabels.Add(new RelativeLabel
                            {
                                Address = PC,
                                RootLineNumber = RootLineNumber
                            });
                            AddOutput(CodeType.Label);
                        }
                        else
                        {
                            bool local = label.StartsWith(".");
                            if (local)
                                label = label.Substring(1) + "@" + ExpressionEngine.LastGlobalLabel;
                            bool valid = true;
                            for (int k = 0; k < label.Length; k++) // Validate label
                            {
                                if (!char.IsLetterOrDigit(label[k]) && label[k] != '_')
                                {
                                    if (local && label[k] == '@')
                                        continue;
                                    valid = false;
                                    break;
                                }
                            }
                            if (!valid)
                                AddError(CodeType.Label, AssemblyError.InvalidLabel);
                            else if (ExpressionEngine.Symbols.ContainsKey(label.ToLower()))
                                AddError(CodeType.Label, AssemblyError.DuplicateName);
                            else
                            {
                                AddOutput(CodeType.Label);
								ExpressionEngine.Symbols.Add(label.ToLower(), new Symbol(PC, true, ORGsList.Last().Subpage));
                                if (!local)
                                    ExpressionEngine.LastGlobalLabel = label.ToLower();
                            }
                        }
                        CurrentLine = CurrentLine.Substring(length + 1).Trim();
                    }
                }

				/// Cesc: quina mena de declaracio d'etiqueta es :LABEL ??
                if (CurrentLine.StartsWith(":") || CurrentLine.EndsWith(":")) // Label
                {
                    string label;
                    if (CurrentLine.StartsWith(":"))
                        label = CurrentLine.Substring(1).Trim();
                    else
                        label = CurrentLine.Remove(CurrentLine.Length - 1).Trim();
                    label = label.ToLower();
                    if (label == "_")
                    {
                        // Relative
                        ExpressionEngine.RelativeLabels.Add(new RelativeLabel
                        {
                            Address = PC,
                            RootLineNumber = RootLineNumber
                        });
                        AddOutput(CodeType.Label);
                    }
                    else
                    {
                        bool local = label.StartsWith(".");
                        if (local)
                            label = label.Substring(1) + "@" + ExpressionEngine.LastGlobalLabel;
                        bool valid = true;
                        for (int k = 0; k < label.Length; k++) // Validate label
                        {
                            if (!char.IsLetterOrDigit(label[k]) && label[k] != '_')
                            {
                                if (local && label[k] == '@')
                                    continue;
                                valid = false;
                                break;
                            }
                        }
                        if (!valid)
                            AddError(CodeType.Label, AssemblyError.InvalidLabel);
                        else if (ExpressionEngine.Symbols.ContainsKey(label.ToLower()))
                            AddError(CodeType.Label, AssemblyError.DuplicateName);
                        else
                        {
                            AddOutput(CodeType.Label);
							ExpressionEngine.Symbols.Add(label.ToLower(), new Symbol(PC, true, ORGsList.Last().Subpage));
                            if (!local)
                                ExpressionEngine.LastGlobalLabel = label.ToLower();
                        }
                    }
                    continue;
                }

                if (CurrentLine.SafeContains('\\'))
                {
                    // Split lines up
                    var split = CurrentLine.SafeSplit('\\');
                    Lines = Lines.Take(CurrentIndex).Concat(split).
                        Concat(Lines.Skip(CurrentIndex + 1)).ToArray();
                    SuspendedLines = split.Length;
                    CurrentIndex--;
                    continue;
                }
					
                if (CurrentLine.StartsWith(".") || CurrentLine.StartsWith("#")) // Directive
                {					
                    // Some directives need to be handled higher up
					var directive = CurrentLine.Substring(1).Trim();
					//Cesc
					//if(directive.IndexOf("org") != -1 || directive.IndexOf("block") != -1)
					//	Console.WriteLine(directive);

                    string[] parameters = new string[0];
                    if (directive.SafeIndexOf(' ') != -1)
                        parameters = directive.Substring(directive.SafeIndexOf(' ')).Trim().SafeSplit(' ');
                    if (directive.ToLower().StartsWith("macro"))
                    {
                        var definitionLine = CurrentLine; // Used to update the listing later
                        if (parameters.Length == 0)
                        {
                            AddError(CodeType.Directive, AssemblyError.InvalidDirective);
                            continue;
                        }
                        string definition = directive.Substring(directive.SafeIndexOf(' ')).Trim();
                        var macro = new Macro();
                        if (definition.Contains("("))
                        {
                            var parameterDefinition = definition.Substring(definition.SafeIndexOf('(') + 1);
                            parameterDefinition = parameterDefinition.Remove(parameterDefinition.SafeIndexOf(')'));
                            // NOTE: This probably introduces the ability to use ".macro foo(bar)this_doesnt_cause_errors"
                            if (string.IsNullOrEmpty(parameterDefinition))
                                macro.Parameters = new string[0];
                            else
                                macro.Parameters = parameterDefinition.SafeSplit(',');
                            macro.Name = definition.Remove(definition.SafeIndexOf('(')).ToLower();
                        }
                        else
                            macro.Name = definition.ToLower(); // TODO: Consider enforcing character usage restrictions
                        for (CurrentIndex++; CurrentIndex < Lines.Length; CurrentIndex++)
                        {
                            CurrentLine = Lines[CurrentIndex].Trim().TrimComments();
                            LineNumbers.Push(LineNumbers.Pop() + 1);
                            RootLineNumber++;
                            if (CurrentLine == ".endmacro" || CurrentLine == "#endmacro")
                                break;
                            macro.Code += CurrentLine + Environment.NewLine;
                        }
                        macro.Code = macro.Code.Remove(macro.Code.Length - Environment.NewLine.Length);
                        macro.Name = macro.Name.ToLower();
                        if (Macros.Any(m => m.Name == macro.Name && m.Parameters.Length == macro.Parameters.Length))
                        {
                            AddError(CodeType.Label, AssemblyError.DuplicateName);
                            continue;
                        }
                        Macros.Add(macro);
                        // Add an entry to the listing
                        Output.Listing.Add(new Listing
                        {
                            Code = definitionLine,
                            CodeType = CodeType.Directive,
                            Error = AssemblyError.None,
                            Warning = AssemblyWarning.None,
                            Address = PC,
                            FileName = FileNames.Peek(),
                            LineNumber = LineNumbers.Peek(),
                            RootLineNumber = RootLineNumber
                        });
                    }
                    else
                    {
                        var result = HandleDirective(CurrentLine);
						if (IsMegaROM && PC < 0xC000 && PC > currentSubpageORG + 0x2000) {
							AddError(CodeType.Label, AssemblyError.MegaROMSubpageOverflow);
							if(Settings.Verbose == VerboseLevels.Diagnostic)
								Console.WriteLine ("{0} {1} Subpage overflow: PC {2:X} {3:X}", FileNames.Peek (), LineNumbers.Peek (), PC, currentSubpageORG);
						}
                        if (result != null)
                            Output.Listing.Add(result);
                    }
                    continue;
                }
                else
                {
					if (string.IsNullOrEmpty (CurrentLine) || !Listing) {
						continue;
					} else 
					{
						if(Settings.Verbose == VerboseLevels.Diagnostic)
							Console.WriteLine ("0x{0:X} {1:D6} {2}",PC, RootLineNumber, CurrentLine);

						// Check instructions
						var match = InstructionSet.Match (CurrentLine);
						if (match == null)
							AddError (CodeType.Instruction, AssemblyError.InvalidInstruction); // Unknown instruction
                    else {
							// Instruction to be fully assembled in the next pass
							Output.Listing.Add (new Listing {
								Code = CurrentLine,
								CodeType = CodeType.Instruction,
								Error = AssemblyError.None,
								Warning = AssemblyWarning.None,
								Instruction = match,
								Address = PC,
								FileName = FileNames.Peek (),
								LineNumber = LineNumbers.Peek (),
								RootLineNumber = RootLineNumber
							});
	
							if (IsMegaROM && PC < 0xC000 && PC +match.Length > currentSubpageORG + 0x2000) {
								if(Settings.Verbose == VerboseLevels.Diagnostic)
									Console.WriteLine ("{0} {1} Error: Subpage overflow: PC {2:X} {3:X}", 
										FileNames.Peek (), LineNumbers.Peek (), PC, currentSubpageORG);

								AddError (CodeType.Instruction, AssemblyError.MegaROMSubpageOverflow); // megaROM subpage overflow
							}
							PC += match.Length;
						}
					}
                }
            }
			addToORGList(PC, 0,0);
			ORGsList.Remove (ORGsList.Last ());
			if (ROMStartLabel != "") {
				ROMStart = (uint)ExpressionEngine.Evaluate(ROMStartLabel, PC, RootLineNumber);							
			}
            return Finish(Output);
        }

        private void AddOutput(CodeType type)
        {
            Output.Listing.Add(new Listing
            {
                Code = CurrentLine,
                CodeType = type,
                Error = AssemblyError.None,
                Warning = AssemblyWarning.None,
                Address = PC,
                FileName = FileNames.Peek(),
                LineNumber = LineNumbers.Peek(),
                RootLineNumber = RootLineNumber
            });
        }

        private void AddError(CodeType type, AssemblyError error)
        {
            Output.Listing.Add(new Listing
            {
                Code = CurrentLine,
                CodeType = type,
                Error = error,
                Warning = AssemblyWarning.None,
                Address = PC,
                FileName = FileNames.Peek(),
                LineNumber = LineNumbers.Peek(),
                RootLineNumber = RootLineNumber
            });
        }

        private AssemblyOutput Finish(AssemblyOutput output)
        {
            var finalBinary = new List<byte>();
            ExpressionEngine.LastGlobalLabel = null;
			/*foreach(ORGItem item in ORGsList)
			{
				Console.WriteLine (" ORG: 0x{0:X}-0x{1:X}", item.ORGAdress, item.ORGLength);				
			}*/

            for (int i = 0; i < output.Listing.Count; i++)
            {
                var entry = output.Listing[i];
				/// Cesc
				//Console.WriteLine(">> {0} {1} {2} \t\t {3}", entry.FileName, entry.LineNumber, entry.Code, entry.Instruction, entry.Output);
                RootLineNumber = entry.RootLineNumber;
                PC = entry.Address;
                LineNumbers = new Stack<int>(new[] { entry.LineNumber });
                if (entry.CodeType == CodeType.Directive)
                {
					// Cesc
					//Console.WriteLine ("Finish directives: " + entry.Code);

					// PassTwo
					if (entry.PostponeEvalulation) {
						/// Cesc
						// Console.WriteLine ("Pass Two: " + entry.Code);
						output.Listing [i] = HandleDirective (entry.Code, true);
					}
						
                    if (output.Listing[i].Output != null)
                        finalBinary.AddRange(output.Listing[i].Output);
                    continue;
                }
                if (entry.Error != AssemblyError.None)
                    continue;
                if (entry.CodeType == CodeType.Label)
                {
                    var name = entry.Code.Remove(entry.Code.IndexOf(':')).Trim(':').ToLower();
                    if (!name.StartsWith(".") && name != "_")
                        ExpressionEngine.LastGlobalLabel = name;
                }
                else if (entry.CodeType == CodeType.Instruction)
                {
                    // Assemble output string
                    string instruction = entry.Instruction.Value.ToLower();
                    foreach (var operand in entry.Instruction.Operands)
                        instruction = instruction.Replace("@" + operand.Key, operand.Value.Value);
                    foreach (var value in entry.Instruction.ImmediateValues)
                    {
                        try
                        {
							bool truncated;
                            if (value.Value.RelativeToPC)
							{
								truncated = false;
								var exp = ExpressionEngine.Evaluate(value.Value.Value, entry.Address, entry.RootLineNumber);
                                instruction = instruction.Replace("^" + value.Key, ConvertToBinary(
									(exp - entry.Instruction.Length) - entry.Address,
									value.Value.Bits, true, out truncated));
							}
                            else if (value.Value.RstOnly)
                            {
                                truncated = false;
                                var rst = (byte)ExpressionEngine.Evaluate(value.Value.Value, entry.Address, entry.RootLineNumber);
                                if ((rst & ~0x7) != rst || rst > 0x38)
                                    entry.Error = AssemblyError.InvalidExpression;
                                else
                                {
                                    instruction = instruction.Replace("&" + value.Key,
										ConvertToBinary((ulong)rst >> 3, 3, false, out truncated));
                                }
                            }
                            else
							{
								truncated = false;
                                instruction = instruction.Replace("%" + value.Key, ConvertToBinary(
                                    ExpressionEngine.Evaluate(value.Value.Value, entry.Address, entry.RootLineNumber),
									value.Value.Bits, false, out truncated));
							}
							if (truncated){
								/// Cesc: 25-8-2016
								/// Cal identificar on l'adreça exedeix 127 per els salts relatius com JR o DJNZ
								/// El problema es que aixo no contempla salts negatius de -1 a -127 (128 a 255)
								/// El segon problema es que avalua de forma diferent ld a,-8 i ld a,VALNEGATIU definit com a VALNEGATIU .equ -8
								/// Tot plegat a causa del metode ConvertToBinary(...) que no te una implementacio gaire bona. 
                                //entry.Error = AssemblyError.ValueTruncated;
								entry.Error = AssemblyError.RelativeAddressTooLong;
								//entry.Warning = AssemblyWarning.ValueTruncated;
							}
                        }
                        catch (KeyNotFoundException)
                        {
                            entry.Error = AssemblyError.UnknownSymbol;
                        }
                        catch (InvalidOperationException)
                        {
                            entry.Error = AssemblyError.InvalidExpression;
                        }
                    }
                    if (entry.Error == AssemblyError.None)
                    {
                        entry.Output = ExpressionEngine.ConvertFromBinary(instruction);
                        finalBinary.AddRange(entry.Output);
                    }
                    else
                        finalBinary.AddRange(new byte[entry.Instruction.Length]);
                }
            }
            output.Data = finalBinary.ToArray();
            return output;
        }

		public static string ConvertToBinary(ulong value, int bits, bool signed, out bool truncated) // Little endian octets
        {
            uint mask = 1;
            string result = "";
            uint truncationMask = 1;
            for (int i = 0; i < bits; i++)
            {
                truncationMask <<= 1;
                truncationMask |= 1;
                if ((value & mask) == mask)
                    result = "1" + result;
                else
                    result = "0" + result;
                mask <<= 1;
            }
			truncationMask >>= 1;
			if (signed)
			{
				int _value = (int)value;
				truncated = 1 << (bits - 1) < Math.Abs (_value);
			}
			else
			{
				int _value = (int)value;
				truncated = 1 << bits < (Math.Abs(_value) + 1);
			}
	
            // Convert to little endian
            if (result.Length % 8 != 0)
                return result;
            string little = "";
            for (int i = 0; i < result.Length; i += 8)
                little = result.Substring(i, 8) + little;
            return little;
        }

		private void addToORGList(uint currentPC, uint PC, uint subpage)
		{
			//ORGsList.Add(new ORGItem(paramter, currentPC));
			if (ORGsList.Count > 0) {
				ORGItem last = ORGsList.Last ();
				ORGsList.Remove (last);
				ORGsList.Add (new ORGItem (last.ORGAdress, currentPC, last.Subpage));
				ORGsList.Add (new ORGItem (PC, PC, subpage));
			} else { 
				ORGsList.Add (new ORGItem (PC, currentPC, subpage));
			}
		}

        #region Directives

        private Listing HandleDirective(string line, bool passTwo = false)
        {
			// Cesc debug directives
			//Console.WriteLine (line);

            string directive = line.Substring(1).Trim();
            string[] parameters = new string[0];
            string parameter = "";
            if (directive.SafeContains(' '))
            {
                parameter = directive.Substring(directive.SafeIndexOf(' ') + 1);
                parameters = parameter.SafeSplit(' ');
                directive = directive.Remove(directive.SafeIndexOf(' '));
            }
            directive = directive.ToLower();
            var listing = new Listing
            {
                Code = line,
                CodeType = CodeType.Directive,
                Address = PC,
                Error = AssemblyError.None,
                Warning = AssemblyWarning.None,
                FileName = FileNames.Peek(),
                LineNumber = LineNumbers.Peek(),
                RootLineNumber = RootLineNumber
            };

			// Cesc
			// if (passTwo) Console.WriteLine ("Pass Two");
            try
            {
                switch (directive)
                {
					case "rom":
					{
						if (parameters.Length != 0)
						{
							listing.Error = AssemblyError.InvalidDirective;
							return listing;
						}
						else
						{
							IsROM = true;
							addToORGList(PC, 0x4000,0);
							PC =0x4000;
							currentSubpageORG = PC;

							return listing;
						}
					}
					case "megarom":
					{
					if (parameters.Length == 0)
						{
							listing.Error = AssemblyError.InvalidDirective;
							return listing;
						}
						else
						{
							// Konami:subpagina de 8 KB,límite de 32 pag.Entre 4000h-5FFFh esta necesariamente la subpágina 0,no puede cambiarse.
							// KonamiSCC: subpágina de 8 KB, límite de 64 páginas. Limite de 512 KB (4 megabits). Soporta acceso a SCC.
							// ASCII8: tamaño de subpágina de 8 KB, límite de 256 pag. Maximo megaROM de 2048 KB (16 megabits, 2 megabytes).
							// ASCII16: subpágina de 16 KB, límite de 256 paginas. El tamaño máximo del megaROM sera 4096 KB (32 megabits).
							IsMegaROM = true;

							if(parameter.Trim().ToLower() == "konamiscc" || 
								parameter.Trim().ToLower() == "konamiscc" || 
								parameter.Trim().ToLower() == "ascii8")
							{
								MegaROMPageSize =8;	
							}
							addToORGList(PC, 0x4000,0);
							PC =0x4000;
							currentSubpageORG = PC;

							return listing;
						}
					}
					case "start":
					{
						if (parameters.Length != 0){
							ROMStartLabel = parameter;
							PC +=16;
						}
						else{
							listing.Error = AssemblyError.InvalidDirective;
						}
						return listing;
					}
					case "block":
                    {
                        ulong amount = ExpressionEngine.Evaluate(parameter, PC, RootLineNumber);
                        listing.Output = new byte[amount];
                        PC += (uint)amount;
                        return listing;
                    }
                    case "byte":
                    case "db":
                    {
                        if (passTwo)
                        {
                            var result = new List<byte>();
                            parameters = parameter.SafeSplit(',');
                            foreach (var p in parameters)
                            {
                                if (p.Trim().StartsWith("\"") && p.Trim().EndsWith("\""))
                                    result.AddRange(Settings.Encoding.GetBytes(p.Trim().Substring(1, p.Trim().Length - 2).Unescape()));
                                else
                                {
                                    try
                                    {
										result.Add((byte)ExpressionEngine.Evaluate(p, PC++, RootLineNumber));
										
                                    }
                                    catch (KeyNotFoundException)
                                    {
                                        listing.Error = AssemblyError.UnknownSymbol;
                                    }
                                }
                            }
                            listing.Output = result.ToArray();
                            return listing;
                        }
                        else
                        {
                            parameters = parameter.SafeSplit(',');
                            int length = 0;
                            foreach (var p in parameters)
                            {
								var para = p.Trim();
								if (para.StartsWith("\"") && para.EndsWith("\""))
									length += para.Substring(1, para.Length - 2).Unescape().Length;
                                else
                                    length++;
                            }
                            listing.Output = new byte[length];
                            listing.PostponeEvalulation = true;
                            PC += (uint)listing.Output.Length;
                            return listing;
                        }
                    }
                    case "word":
                    case "dw":
                    {
                        if (passTwo)
                        {
                            var result = new List<byte>();
                            parameters = parameter.SafeSplit(',');
                            foreach (var item in parameters)
                                result.AddRange(TruncateWord(ExpressionEngine.Evaluate(item, PC++, RootLineNumber)));
                            listing.Output = result.ToArray();
                            return listing;
                        }
                        else
                        {
							// Cesc: resoldre el bug .dw 0,0,0 
							parameters = parameter.SafeSplit(',');

                            listing.Output = new byte[parameters.Length * (InstructionSet.WordSize / 8)];
                            listing.PostponeEvalulation = true;
                            PC += (uint)listing.Output.Length;
                            return listing;
                        }
                    }
                    case "error":
                    case "echo":		// Cesc TODO: cal formatar en cada cas
					case "print":		// Cesc TODO: cal formatar en cada cas
					case "printdec":	// Cesc TODO: cal formatar en cada cas
					case "printtext":
                    {
						if(Settings.Verbose != VerboseLevels.Quiet &&
							Settings.Verbose != VerboseLevels.Diagnostic)
						{
	                        if (passTwo)
	                        {
	                            string output = "";
	                            bool formatOutput = false;
	                            List<object> formatParameters = new List<object>();
	                            foreach (var item in parameters)
	                            {
	                                if (item.Trim().StartsWith("\"") && item.EndsWith("\""))
	                                {
	                                    output += item.Substring(1, item.Length - 2);
	                                    formatOutput = true;
	                                }
	                                else
	                                {
	                                    if (!formatOutput)
	                                        output += ExpressionEngine.Evaluate(item, PC, RootLineNumber);
	                                    else
	                                    {
	                                        formatParameters.Add(ExpressionEngine.Evaluate(item, PC, RootLineNumber));
	                                    }
	                                }
	                            }
	                            if (formatOutput)
	                                output = string.Format(output, formatParameters.ToArray());
	                            Console.WriteLine((directive == "error" ? "User Error: " : "") + output);
	                            return listing;
	                        }
	                        else
	                        {
	                            listing.PostponeEvalulation = true;
	                            return listing;
	                        }
						}
						break;
                    }
                    case "end":
					{
						// end
                        return listing;
					}
					/// Cesc
					case "ds":
					// TODO: cal contemplar el subpage overflow
					case "fill":
                    {
                        parameters = parameter.SafeSplit(',');
                        ulong amount = ExpressionEngine.Evaluate(parameters[0], PC, RootLineNumber);
                        if (parameters.Length == 1)
                        {
                            Array.Resize<string>(ref parameters, 2);
                            parameters[1] = "0";
                        }
						//Cesc
						if(amount <= 0xffff)
						{
							/*if (IsMegaROM && PC < 0xC000 && PC +amount > currentSubpageORG + 0x2000) {
								Console.WriteLine ("{0} {1} Subpage overflow: PC {2:X} {3:X}", FileNames.Peek (), LineNumbers.Peek (), PC, currentSubpageORG);
							}*/
                            listing.Output = new byte[amount];
                            for (int i = 0; i < (int)amount; i++)
                                listing.Output[i] = (byte)ExpressionEngine.Evaluate(parameters[1], PC++, RootLineNumber);
                            
						}
						else
						{
							Console.WriteLine(".fill or .ds too big:{0} at line:{1}, param:{2}",
								amount, RootLineNumber, parameter);
						}
						return listing;
                    }
                    case "org":
					{
						if(IsMegaROM)
						{
							uint now = (uint)ExpressionEngine.Evaluate(parameter, PC, RootLineNumber);

							if ( now >= 0xc000 || now  < currentSubpageORG +0x2000)
							{
								uint currentPC = PC;
								PC = (uint)ExpressionEngine.Evaluate(parameter, PC, RootLineNumber);

								if(Settings.Verbose == VerboseLevels.Diagnostic)
									Console.WriteLine("<< Current PC:{3:X}, new PC:{0:X}, parameter:{1}, RootLineNumber:{2} >>", 
									PC, parameter, RootLineNumber,currentPC);

								addToORGList(currentPC, PC,0);
							}
							else
							{
								/// TODO: aixo no es del tot correcte, si el ORG esta dins la pagina pero el codi
								/// posterior creix fora d'aquesta tindrem un problema :p
								uint currentPC = PC;
								PC = (uint)ExpressionEngine.Evaluate(parameter, PC, RootLineNumber);
								if(Settings.Verbose == VerboseLevels.Diagnostic){
									Console.WriteLine("<< Subpage Overflow, Current PC:{3:X}, new PC:{0:X}, parameter:{1}, RootLineNumber:{2}, now {3:X} >>", 
									PC, parameter, RootLineNumber,currentPC, now);
								}
								listing.Error = AssemblyError.MegaROMSubpageOverflow;  // megaROM subpage overflow
							}
						}
						else
						{
							uint currentPC = PC;
	                        PC = (uint)ExpressionEngine.Evaluate(parameter, PC, RootLineNumber);

							if(Settings.Verbose == VerboseLevels.Diagnostic)
								Console.WriteLine("<< Current PC:{3:X}, new PC:{0:X}, parameter:{1}, RootLineNumber:{2} >>", 
									PC, parameter, RootLineNumber,currentPC);
							addToORGList(currentPC, PC,0);
						}
                        return listing;
					}
                    case "page":
					{
						uint currentPC = PC;
                        ulong page = ExpressionEngine.Evaluate(parameter, PC, RootLineNumber);
                        switch (page)
                        {
                            case 0:
                                PC = 0;
                                break;
                            case 1:
                                PC = 0x4000;
                                break;
                            case 2:
                                PC = 0x8000;
                                break;
                            case 3:
                                PC = 0xc000;
                                break;
                        }
						// Cesc TODO el org encara no es comporta com esperem ?
						if(Settings.Verbose == VerboseLevels.Diagnostic)
							Console.WriteLine("<< Current PC:{3:X}, new PC:{0:X}, parameter:{1}, RootLineNumber:{2} >>", 
								PC, parameter, RootLineNumber, currentPC);
						
						addToORGList(currentPC, PC,0);
                        return listing;
					}

					/// .subpage 15 at $A000	
					case "subpage":
					{
						if (parameters.Length != 4){
							if(Settings.Verbose == VerboseLevels.Diagnostic)
								Console.WriteLine(".subpage {0} {1} {2}",parameters[0], parameters[1], parameters[2]);

							uint currentPC = PC;
							PC = (uint)ExpressionEngine.Evaluate(parameters[2], PC, RootLineNumber);
							currentSubpageORG = PC;
							uint subpage = (uint)ExpressionEngine.Evaluate(parameters[0], PC, RootLineNumber);
							addToORGList(currentPC, PC, subpage);
						}
						else{
							listing.Error = AssemblyError.InvalidDirective;
						}
						return listing;
					}

					case "verbose":
					{
						Settings.Verbose = VerboseLevels.Normal;
						break;
					}
					case "bios":
                    case "include":
                    {
						if(directive =="bios")
							parameter ="\".\\libs\\bios.asm\"";
						
						string file = GetIncludeFile(parameter);
						
                        if (file == null)
                        {
                            listing.Error = AssemblyError.FileNotFound;
                            return listing;
                        }
                        FileNames.Push(Path.GetFileName(parameter.Substring(1, parameter.Length - 2)));
                        LineNumbers.Push(0);
                        string includedFile = File.ReadAllText(file) + "\n.endfile";

						if(Settings.Verbose != VerboseLevels.Quiet)
							Console.WriteLine("Include: "+file);

						if (ASMSX)
							includedFile = includedFile.Replace ("@@", ".");						
						
						string[] lines = includedFile.Replace("\r", "").Replace('\t',' ').Split('\n');
                        Lines =
                            Lines.Take(CurrentIndex + 1)
                                 .Concat(lines)
                                 .Concat(Lines.Skip(CurrentIndex + 1))
                                 .ToArray();
                        return listing;
                    }
					/// Cesc
					case "incbin":
					{
						if(Settings.Verbose == VerboseLevels.Diagnostic)
						{
							//Console.WriteLine("incbin paramenters {0} parameter {1}",parameters, parameter);
							Console.WriteLine ("0x{0:X} {1:D6} {2}",PC, RootLineNumber, CurrentLine);
						}
						parameters = parameters.Where(w => w != "").ToArray(); 

						string file ="";
						long amountSkip =0;
						long amountSize =0;

						if (parameters.Length > 0)
						{
							file = parameters[0].Trim().Substring(1, parameters[0].Length - 2); // Remove <> or ""
							file = file.Replace("\\","/");

							if (parameters.Length > 1)
							{
								string skipValue ="";
								string sizeValue ="";
								bool skip = false;
								bool size = false;
								foreach(string param in parameters.Skip(1))
								{
									if (param.ToUpper() == "SKIP")
									{
										skip =true;
										size = false;
									}
									else if (param.ToUpper() == "SIZE")
									{
										size = true;
										skip = false;
									}
									else
									{
										if(skip)
											skipValue += param;
										if(size)
											sizeValue += param;
									}
								}
								amountSkip = (long)ExpressionEngine.Evaluate(skipValue, PC, RootLineNumber);
								amountSize = (long)ExpressionEngine.Evaluate(sizeValue, PC, RootLineNumber);
								// Console.WriteLine("skip {0} size {1}",skipValue, sizeValue);
								// Console.WriteLine("skip {0} size {1}",amountSkip, amountSize);
							}
							if(file != "")
							{
								/// Cesc per fer tambe la cerca al directori especificat amb --include
								string inc_file = GetIncludeFile("<"+ file +">");
								if(inc_file != null)
									file = inc_file;

								if (File.Exists(file))
								{
									var fs = new FileStream(file, FileMode.Open);
									try{
										if (amountSkip + amountSize > fs.Length)
											throw new Exception("Lectura fora del fitxer.");

										long fsLength = fs.Length;
										if (amountSize == 0) 
											amountSize = fsLength;
										if(amountSkip >0)
											fs.Seek(amountSkip, SeekOrigin.Begin);
							
										byte[] fileBytes = new byte[amountSize];
										fs.Read(fileBytes, 0, (int)amountSize);
										fs.Close();

										if(Settings.Verbose != VerboseLevels.Quiet)
										{
											if (amountSkip == 0 && amountSize == fsLength) 
												Console.WriteLine("Including binary file {0}",file);
											else
												Console.WriteLine("Including binary file {0} skipping {1} bytes, saving {2} bytes",
													file, amountSkip,amountSize);
										}
										if(Settings.Verbose == VerboseLevels.Diagnostic)
											Console.WriteLine("Readed {0} bytes",fileBytes.Length);
										
										listing.Output = fileBytes; 
										PC += (uint)fileBytes.Length;
										return listing;
									}catch(Exception ex)
									{
										if(Settings.Verbose == VerboseLevels.Diagnostic)
											Console.WriteLine("Error {0}", ex.Message);	
										
										/// TODO: cal un error adequat
										listing.Error = AssemblyError.FileNotFound;
										fs.Close();
									}
								}
								else{
									
									Console.WriteLine("{0} {1} File {2} not found.", RootLineNumber, CurrentLine, file);
									listing.Error = AssemblyError.FileNotFound;
								}
							}
						}
						break;
					}
                    case "endfile": // Special, undocumented directive
					{
                        RootLineNumber--;
                        LineNumbers.Pop();
                        FileNames.Pop();
                        return null;
					}
					case "module":
					{
						moduleNamespace.Push(parameter);
						break;
					}
					case "endmodule":
					{
						if (moduleNamespace.Count >0){
							moduleNamespace.Pop();
						}
						else{
							Console.WriteLine("Error .endmodule: missing .module");
						}
						break;
					}

					case "phase":
					{
						try{
						phasePC = (uint)ExpressionEngine.Evaluate(parameter, PC, RootLineNumber);
						}
						catch(Exception)
						{
							if(Settings.Verbose != VerboseLevels.Quiet)
								Console.WriteLine("PHASE solve error in expression: {0}.", parameter);
						}

						if (phasePC == 0)
						{
							if(Settings.Verbose != VerboseLevels.Quiet)
								Console.WriteLine("ignore PHASE {0} = {1} will be done in pass two.", parameter, phasePC);
							
							listing.PostponeEvalulation = true;
						}
						else
						{
							dephasePC = PC;
							PC = phasePC;
							if(Settings.Verbose != VerboseLevels.Quiet)
								Console.WriteLine("PHASE 0x{0:X} (PC: 0x{1:X})", phasePC, dephasePC);
						}
						return listing;
					}
					case "dephase":
					{
						if (phasePC != 0){
							uint delta =0;

							// Cesc: TODO aixo es extrany, funciona pero cal veure quin es el problema
							if(!passTwo)
								delta = PC - phasePC;
							else
								delta = PC - dephasePC;

							if(Settings.Verbose != VerboseLevels.Quiet)
								Console.WriteLine("DEPHASE 0x{0:X} delta: 0x{1:X} (PC: 0x{2:X})", PC, delta, dephasePC + delta);
							
							PC = dephasePC + delta;
							phasePC =0;
							dephasePC =0;
						}
						else if (passTwo) 
						{
							if(Settings.Verbose != VerboseLevels.Quiet)
								Console.WriteLine("Error .dephase: missing .phase");
						}
						else
						{
							listing.PostponeEvalulation = true;
						}
						return listing;
					}
					case "rept":
					{
						if (!passTwo) 
						{
							PCRepeat = PC;
							repeatAmount = ExpressionEngine.Evaluate(parameter, PC, RootLineNumber);

							// Cesc TODO: aixo ens caldra per fer la segona passada
							//listing.PostponeEvalulation = true;
							return listing;
						}
						else{
							Console.WriteLine ("Pass Two .rept");
							break;
						}
					}
					// cesc TODO
					case "endr":
					{
						if (passTwo) 
							Console.WriteLine ("Pass Two .endr");
						
						if (repeatAmount != 0){
							ulong blockSize = PC - PCRepeat;
							ulong amount = repeatAmount * (blockSize-1);

							var test = Output.Listing.Last().Instruction.Value;

							listing.Output = new byte[amount];

							/*
							//byte[] block = new byte[blockSize]; 
							//listing.Output = new byte[amount];
							List<Listing> block = new List<sass.Listing>();
							ulong listLength = 0;

							int last = Output.Listing.Count-1;
							while(listLength < blockSize)
							{
								listLength += Output.Listing[last].Instruction.Length;
								Console.WriteLine("{0}",Output.Listing[last].Instruction.Length);
								block.Add(Output.Listing[last]);
								last--;
							}

							foreach(var item in block)
							{
							}

							for(ulong i=0;i<repeatAmount;i++)
							{
								//listing.Output = listing.Output[PC-blockSize];
								for(ulong j=0;j<blockSize;j++){
									listing.Output[i*blockSize+j]= 0;
								} 								
							}
							*/
							PC += (uint)amount;
							PCRepeat =0;
							repeatAmount =0;
							//listing.PostponeEvalulation = true;
							return listing;
						}
						else{
							Console.WriteLine("Error .endr: missing .rept");
						}
						break;
					}
                    case "equ":
					{
                        if (parameters.Length == 1)
                        {
                            if (ExpressionEngine.Symbols.ContainsKey(parameters[0].ToLower()))
                            {
                                listing.Error = AssemblyError.DuplicateName;
                                return listing;
                            }
                            ExpressionEngine.Symbols.Add(parameters[0].ToLower(), new Symbol(1));
                        }
                        else
                        {
                            if (ExpressionEngine.Symbols.ContainsKey(parameters[0].ToLower()))
                            {
                                listing.Error = AssemblyError.DuplicateName;
                                return listing;
                            }

							/// Cesc: append module name
							string label = parameters[0].ToLower();
							if (moduleNamespace.Count >0 )
								label = moduleNamespace.First() +"."+label;

							/*foreach (var item in parameters)
								result.AddRange(TruncateWord(ExpressionEngine.Evaluate(item, PC++, RootLineNumber)));
							listing.Output = result.ToArray();
							return listing;*/
			
                            ExpressionEngine.Symbols.Add(label, new Symbol(
								(uint) ExpressionEngine.Evaluate(parameter.Substring(parameter.IndexOf(' ') + 1).Trim(), PC,RootLineNumber),
								ORGsList.Last().Subpage));
                        }
                        return listing;
					}
                    case "exec":
					{
                        if (parameters.Length == 0 || !AllowExec)
                        {
                            listing.Error = AssemblyError.InvalidDirective;
                            return listing;
                        }
                        else
                        {
                            var process = new ProcessStartInfo(parameters[0], string.Join(" ", parameters.Skip(1).ToArray()));
                            process.UseShellExecute = false;
                            process.RedirectStandardOutput = true;
                            var p = Process.Start(process);
                            var output = p.StandardOutput.ReadToEnd().Trim('\n', '\r', ' ');
                            p.WaitForExit();
                            listing.Output = Settings.Encoding.GetBytes(output);
                            PC += (uint)listing.Output.Length;
                            return listing;
                        }
					}
                    case "define":
					{
                        if (parameters.Length == 1)
                        {
                            if (ExpressionEngine.Symbols.ContainsKey(parameters[0].ToLower()))
                            {
                                listing.Error = AssemblyError.DuplicateName;
                                return listing;
                            }
                            ExpressionEngine.Symbols.Add(parameters[0].ToLower(), new Symbol(1));
                        }
                        else
                        {
                            var macro = new Macro();
                            if (parameter.Contains("("))
                            {
                                var parameterDefinition = parameter.Substring(parameter.SafeIndexOf('(') + 1);
                                parameterDefinition = parameterDefinition.Remove(parameterDefinition.SafeIndexOf(')'));
                                // NOTE: This probably introduces the ability to use ".macro foo(bar)this_doesnt_cause_errors"
                                macro.Parameters = parameterDefinition.SafeSplit(',');
                                for (int i = 0; i < macro.Parameters.Length; i++)
                                    macro.Parameters[i] = macro.Parameters[i].Trim();
                                macro.Name = parameter.Remove(parameter.SafeIndexOf('('));
                                macro.Code = parameter.Substring(parameter.SafeIndexOf(')') + 1);
                            }
                            else
                            {
                                macro.Name = parameter.Remove(parameter.SafeIndexOf(' ') + 1);
                                // TODO: Consider enforcing character usage restrictions
                                macro.Code = parameter.Substring(parameter.SafeIndexOf(' ') + 1).Trim();
                            }
                            macro.Name = macro.Name.ToLower().Trim();
                            if (Macros.Any(m => m.Name == macro.Name && m.Parameters.Length == macro.Parameters.Length))
                            {
                                listing.Error = AssemblyError.DuplicateName;
                                return listing;
                            }
                            Macros.Add(macro);
                        }
                        return listing;
					}
                    case "if":
					{
                        if (parameters.Length == 0)
                        {
                            listing.Error = AssemblyError.InvalidDirective;
                            return listing;
                        }
                        if (!IfStack.Peek())
                        {
                            WorkingIfStack.Push(false);
                            return listing;
                        }
                        try
                        {
                            IfStack.Push(ExpressionEngine.Evaluate(parameter, PC, RootLineNumber) != 0);
                        }
                        catch (InvalidOperationException)
                        {
                            listing.Error = AssemblyError.InvalidExpression;
                        }
                        catch (KeyNotFoundException)
                        {
                            listing.Error = AssemblyError.UnknownSymbol;
                        }
                        return listing;
					}
                    case "ifdef":
                    {
                        if (parameters.Length != 1)
                        {
                            listing.Error = AssemblyError.InvalidDirective;
                            return listing;
                        }
                        if (!IfStack.Peek())
                        {
                            WorkingIfStack.Push(false);
                            return listing;
                        }
                        var result = ExpressionEngine.Symbols.ContainsKey(parameters[0].ToLower());
                        if (!result)
                            result = Macros.Any(m => m.Name.ToLower() == parameters[0].ToLower());
                        IfStack.Push(result);
                        return listing;
                    }
                    case "ifndef":
                    {
                        if (parameters.Length != 1)
                        {
                            listing.Error = AssemblyError.InvalidDirective;
                            return listing;
                        }
                        if (!IfStack.Peek())
                        {
                            WorkingIfStack.Push(false);
                            return listing;
                        }
                        var result = ExpressionEngine.Symbols.ContainsKey(parameters[0].ToLower());
                        if (!result)
                            result = Macros.Any(m => m.Name.ToLower() == parameters[0].ToLower());
                        IfStack.Push(!result);
                        return listing;
                    }
                    case "endif":
					{
                        if (parameters.Length != 0)
                        {
                            listing.Error = AssemblyError.InvalidDirective;
                            return listing;
                        }
                        if (IfStack.Count == 1)
                        {
                            listing.Error = AssemblyError.UncoupledStatement;
                            return listing;
                        }
                        if (WorkingIfStack.Any())
                        {
                            WorkingIfStack.Pop();
                            return listing;
                        }
                        IfStack.Pop();
                        return listing;
					}
                    case "else":
					{
                        if (parameters.Length != 0)
                        {
                            listing.Error = AssemblyError.InvalidDirective;
                            return listing;
                        }
                        if (WorkingIfStack.Any())
                            return listing;
                        IfStack.Push(!IfStack.Pop());
                        return listing;
                    //case "elif": // TODO: Requires major logic changes
                    //case "elseif":
                    //    if (IfStack.Peek())
                    //    {
                    //        IfStack.Pop();
                    //        IfStack.Push(false);
                    //        return listing;
                    //    }
                    //    return listing;
					}
                    case "ascii":
					{
                        if (parameters.Length == 0)
                        {
                            listing.Error = AssemblyError.InvalidDirective;
                            return listing;
                        }
                        if (!(parameter.StartsWith("\"") && parameter.EndsWith("\"")))
                        {
                            listing.Error = AssemblyError.InvalidDirective;
                            return listing;
                        }
                        parameter = parameter.Substring(1, parameter.Length - 2);
                        listing.Output = Settings.Encoding.GetBytes(parameter.Unescape());
                        return listing;
					}
                    case "asciiz":
					{
						if (passTwo)
						{
							var result = new List<byte>();
							parameters = parameter.SafeSplit(',');
							foreach (var p in parameters)
							{
								if (p.Trim().StartsWith("\"") && p.Trim().EndsWith("\""))
									result.AddRange(Settings.Encoding.GetBytes(p.Trim().Substring(1, p.Length - 2).Unescape()));
								else
									listing.Error = AssemblyError.InvalidDirective;
							}
							listing.Output = new byte[result.Count + 1];
							Array.Copy(result.ToArray(), listing.Output, result.Count);
							listing.Output[listing.Output.Length - 1] = 0;
							return listing;
						}
						else
						{
							parameters = parameter.SafeSplit(',');
							int length = 0;
							foreach (var p in parameters)
							{
								if (p.StartsWith("\"") && p.EndsWith("\""))
									length += p.Substring(1, p.Length - 2).Unescape().Length;
								else
									listing.Error = AssemblyError.InvalidDirective;
							}
							length++;
							listing.Output = new byte[length];
							listing.PostponeEvalulation = true;
							PC += (uint)listing.Output.Length;
							return listing;
						}
						//break; //Cesc unreachable code
					}
                    case "asciip":
					{
						if (passTwo)
						{
							var result = new List<byte>();
							parameters = parameter.SafeSplit(',');
							foreach (var p in parameters)
							{
								if (p.Trim().StartsWith("\"") && p.Trim().EndsWith("\""))
									result.AddRange(Settings.Encoding.GetBytes(p.Trim().Substring(1, p.Length - 2).Unescape()));
								else
									listing.Error = AssemblyError.InvalidDirective;
							}
							listing.Output = new byte[result.Count + 1];
							listing.Output[0] = (byte)result.Count;
							Array.Copy(result.ToArray(), 0, listing.Output, 1, result.Count);
							return listing;
						}
						else
						{
							parameters = parameter.SafeSplit(',');
							int length = 0;
							foreach (var p in parameters)
							{
								if (p.StartsWith("\"") && p.EndsWith("\""))
									length += p.Substring(1, p.Length - 2).Unescape().Length;
								else
									listing.Error = AssemblyError.InvalidDirective;
							}
							length++;
							listing.Output = new byte[length];
							listing.PostponeEvalulation = true;
							PC += (uint)listing.Output.Length;
							return listing;
						}
						//break; //Cesc unrecheable code
					}
                    case "nolist":
					{
                        Listing = false;
                        return listing;
					}
                    case "list":
					{
                        Listing = true;
                        return listing;
					}
                    case "undefine":
					{
                        if (parameters.Length == 0)
                        {
                            listing.Error = AssemblyError.InvalidDirective;
                            return listing;
                        }
                        foreach (var item in parameters)
                        {
                            if (Macros.Any(m => m.Name == item.ToLower()))
                                Macros.Remove(Macros.FirstOrDefault(m => m.Name == item));
                            else if (ExpressionEngine.Symbols.ContainsKey(item.ToLower()))
                                ExpressionEngine.Symbols.Remove(item.ToLower());
                            else
                            {
                                listing.Error = AssemblyError.UnknownSymbol;
                                return listing;
                            }
                        }
                        return listing;
					}
					default:
					Console.WriteLine("Invalid directive .{0}  {1:D6} {2}", directive, RootLineNumber, CurrentLine);
						break;
				}
            }
            catch (KeyNotFoundException)
            {
                listing.Error = AssemblyError.UnknownSymbol;
                return listing;
            }
            catch (InvalidOperationException)
            {
                listing.Error = AssemblyError.InvalidExpression;
                return listing;
            }
			// Cesc
			catch(Exception ex) {
                if (Settings.Verbose != VerboseLevels.Quiet)
                {
                    Console.WriteLine("Cesc Exception: {0}", ex.Message);
                    Console.WriteLine(" in:  [{0}]", line);
                }
			}
            return null;
        }


        private string GetIncludeFile(string file)
        {
            file = file.Substring(1, file.Length - 2); // Remove <> or ""
			file = file.Replace("\\","/");
            if (File.Exists(file))
                return file;
            foreach (var path in Settings.IncludePath)
            {
                if (File.Exists(Path.Combine(path, file)))
                    return Path.Combine(path, file);
            }
            return null;
        }

        private byte[] TruncateWord(ulong value)
        {
            var array = BitConverter.GetBytes(value);
            return array.Take(InstructionSet.WordSize / 8).ToArray();
        }
			
        #endregion
    }
}
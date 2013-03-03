﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace sass
{
    public class Assembler
    {
        public InstructionSet InstructionSet { get; set; }
        public ExpressionEngine ExpressionEngine { get; set; }
        public AssemblyOutput Output { get; set; }
        public Encoding Encoding { get; set; }
        public List<string> IncludePaths { get; set; }
        public List<Macro> Macros { get; set; }

        private uint PC { get; set; }
        private string[] Lines { get; set; }
        private int RootLineNumber { get; set; }
        private Stack<int> LineNumbers { get; set; }
        private Stack<string> FileNames { get; set; }
        private Stack<bool> IfStack { get; set; }
        private int SuspendedLines { get; set; }
        private int CurrentIndex { get; set; }
        private string CurrentLine { get; set; }

        public Assembler(InstructionSet instructionSet)
        {
            InstructionSet = instructionSet;
            ExpressionEngine = new ExpressionEngine();
            SuspendedLines = 0;
            LineNumbers = new Stack<int>();
            FileNames = new Stack<string>();
            IncludePaths = new List<string>();
            Macros = new List<Macro>();
        }

        public AssemblyOutput Assemble(string assembly, string fileName = null)
        {
            Output = new AssemblyOutput();
            Output.InstructionSet = InstructionSet;
            assembly = assembly.Replace("\r", "");
            PC = 0;
            Lines = assembly.Split('\n');
            FileNames.Push(fileName);
            LineNumbers.Push(0);
            RootLineNumber = 0;
            for (CurrentIndex = 0; CurrentIndex < Lines.Length; CurrentIndex++)
            {
                CurrentLine = Lines[CurrentIndex].Trim().TrimComments().ToLower();
                if (SuspendedLines == 0)
                {
                    LineNumbers.Push(LineNumbers.Pop() + 1);
                    RootLineNumber++;
                }
                else
                    SuspendedLines--;

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

                if (CurrentLine.SafeContains(".equ") && !CurrentLine.StartsWith(".equ"))
                {
                    var name = CurrentLine.Remove(CurrentLine.SafeIndexOf(".equ"));
                    var definition = CurrentLine.Substring(CurrentLine.SafeIndexOf(".equ") + 4);
                    CurrentLine = ".equ " + name.Trim() + ", " + definition.Trim();
                }

                if (CurrentLine.StartsWith(".") || CurrentLine.StartsWith("#")) // Directive
                {
                    // Some directives need to be handled higher up
                    var directive = CurrentLine.Substring(1).Trim().ToLower();
                    string[] parameters = new string[0];
                    if (directive.SafeIndexOf(' ') != -1)
                        parameters = directive.Substring(directive.SafeIndexOf(' ')).Split(',');
                    if (directive.StartsWith("macro"))
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
                            macro.Parameters = parameterDefinition.SafeSplit(',');
                            macro.Name = definition.Remove(definition.SafeIndexOf('('));
                        }
                        else
                            macro.Name = definition; // TODO: Consider enforcing character usage restrictions
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
                        if (Macros.Any(m => m.Name == macro.Name))
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
                    else if (directive.StartsWith("include"))
                    {
                        if (parameters.Length == 0)
                        {
                            AddError(CodeType.Directive, AssemblyError.InvalidDirective);
                            continue;
                        }

                    }
                    else
                    {
                        var result = HandleDirective(CurrentLine);
                        if (result != null)
                            Output.Listing.Add(result);
                    }
                    continue;
                }
                else if (CurrentLine.StartsWith(":") || CurrentLine.EndsWith(":")) // Label
                {
                    string label;
                    if (CurrentLine.StartsWith(":"))
                        label = CurrentLine.Substring(1).Trim();
                    else
                        label = CurrentLine.Remove(CurrentLine.Length - 1).Trim();
                    label = label.ToLower();
                    bool valid = true;
                    for (int k = 0; k < label.Length; k++) // Validate label
                    {
                        if (!char.IsLetterOrDigit(label[k]) && k != '_')
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (!valid)
                        AddError(CodeType.Label, AssemblyError.InvalidLabel);
                    else
                    {
                        Output.Listing.Add(new Listing
                        {
                            Code = CurrentLine,
                            CodeType = CodeType.Label,
                            Error = AssemblyError.None,
                            Warning = AssemblyWarning.None,
                            Address = PC,
                            FileName = FileNames.Peek(),
                            LineNumber = LineNumbers.Peek(),
                            RootLineNumber = RootLineNumber
                        });
                        ExpressionEngine.Equates.Add(label, PC);
                    }
                }
                else
                {
                    // Check for macro
                    Macro macroMatch = null;
                    string[] parameters = null;
                    string parameterDefinition = null;
                    foreach (var macro in Macros)
                    {
                        if (CurrentLine.SafeContains(macro.Name))
                        {
                            // Try to match
                            int startIndex = CurrentLine.SafeIndexOf(macro.Name);
                            int endIndex = startIndex + macro.Name.Length - 1;
                            if (macro.Parameters.Length != 0)
                            {
                                if (CurrentLine[endIndex + 1] != '(')
                                    continue;
                                parameterDefinition = CurrentLine.Substring(endIndex + 2, CurrentLine.SafeIndexOf(')') - (endIndex + 2));
                                parameters = parameterDefinition.SafeSplit(',');
                                if (parameters.Length != macro.Parameters.Length)
                                    continue;
                                // Matched
                                macroMatch = macro;
                                break;
                            }
                        }
                    }
                    if (macroMatch != null)
                    {
                        // Add an entry to the listing
                        AddOutput(CodeType.Directive);
                        var code = macroMatch.Code;
                        int index = 0;
                        foreach (var parameter in macroMatch.Parameters)
                            code = code.Replace(parameter, parameters[index++]);
                        string newLine;
                        if (parameterDefinition != null)
                            newLine = CurrentLine.Replace(macroMatch.Name + "(" + parameterDefinition + ")", code);
                        else
                            newLine = CurrentLine.Replace(macroMatch.Name, code);
                        var newLines = newLine.Replace("\r\n", "\n").Split('\n');
                        SuspendedLines += newLines.Length;
                        // Insert macro
                        Lines = Lines.Take(CurrentIndex).Concat(newLines).Concat(Lines.Skip(CurrentIndex + 1)).ToArray();
                        CurrentIndex--;
                        continue;
                    }
                    if (string.IsNullOrEmpty(CurrentLine))
                        continue;
                    // Check instructions
                    var match = InstructionSet.Match(CurrentLine);
                    if (match == null)
                        AddError(CodeType.Instruction, AssemblyError.InvalidInstruction); // Unknown instruction
                    else
                    {
                        // Instruction to be fully assembled in the next pass
                        Output.Listing.Add(new Listing
                        {
                            Code = CurrentLine,
                            CodeType = CodeType.Instruction,
                            Error = AssemblyError.None,
                            Warning = AssemblyWarning.None,
                            Instruction = match,
                            Address = PC,
                            FileName = FileNames.Peek(),
                            LineNumber = LineNumbers.Peek(),
                            RootLineNumber = RootLineNumber
                        });
                        PC += match.Length;
                    }
                }
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
            List<byte> finalBinary = new List<byte>();
            for (int i = 0; i < output.Listing.Count; i++)
            {
                var entry = output.Listing[i];
                RootLineNumber = entry.RootLineNumber;
                PC = entry.Address;
                LineNumbers = new Stack<int>(new[] { entry.LineNumber });
                if (entry.CodeType == CodeType.Directive)
                {
                    if (entry.PostponeEvalulation)
                        output.Listing[i] = HandleDirective(entry.Code, true);
                    if (output.Listing[i].Output != null)
                        finalBinary.AddRange(output.Listing[i].Output);
                    continue;
                }
                if (entry.Error != AssemblyError.None)
                    continue;
                if (entry.CodeType == CodeType.Instruction)
                {
                    // Assemble output string
                    string instruction = entry.Instruction.Value.ToLower();
                    foreach (var operand in entry.Instruction.Operands)
                        instruction = instruction.Replace("@" + operand.Key, operand.Value.Value);
                    foreach (var value in entry.Instruction.ImmediateValues)
                    {
                        // TODO: Truncation warning
                        if (value.Value.RelativeToPC)
                            instruction = instruction.Replace("^" + value.Key, ConvertToBinary(
                                entry.Address -
                                (ExpressionEngine.Evaluate(value.Value.Value, entry.Address) + entry.Instruction.Length),
                                value.Value.Bits));
                        else
                            instruction = instruction.Replace("%" + value.Key, ConvertToBinary(
                                ExpressionEngine.Evaluate(value.Value.Value, entry.Address),
                                value.Value.Bits));
                    }
                    entry.Output = ExpressionEngine.ConvertFromBinary(instruction);
                    finalBinary.AddRange(entry.Output);
                }
            }
            output.Data = finalBinary.ToArray();
            return output;
        }

        private static string ConvertToBinary(ulong value, int bits) // Little endian octets
        {
            ulong mask = 1;
            string result = "";
            for (int i = 0; i < bits; i++)
            {
                if ((value & mask) == mask)
                    result = "1" + result;
                else
                    result = "0" + result;
                mask <<= 1;
            }
            // Convert to little endian
            string little = "";
            for (int i = 0; i < result.Length; i += 8)
                little = result.Substring(i, 8) + little;
            return little;
        }

        #region Directives

        private Listing HandleDirective(string line, bool passTwo = false)
        {
            string directive = line.Substring(1).Trim();
            string[] parameters = new string[0];
            string parameter = "";
            if (directive.SafeContains(' '))
            {
                parameter = directive.Substring(directive.SafeIndexOf(' ') + 1);
                parameters = parameter.SafeSplit(',');
                directive = directive.Remove(directive.SafeIndexOf(' '));
            }
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
            switch (directive)
            {
                case "block":
                {
                    ulong amount = ExpressionEngine.Evaluate(parameter, PC);
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
                        foreach (var item in parameters)
                            result.Add((byte)ExpressionEngine.Evaluate(item, PC++));
                        listing.Output = result.ToArray();
                        return listing;
                    }
                    else
                    {
                        listing.Output = new byte[parameters.Length];
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
                        foreach (var item in parameters)
                            result.AddRange(TruncateWord(ExpressionEngine.Evaluate(item, PC++)));
                        listing.Output = result.ToArray();
                        return listing;
                    }
                    else
                    {
                        listing.Output = new byte[parameters.Length * (InstructionSet.WordSize / 8)];
                        listing.PostponeEvalulation = true;
                        PC += (uint)listing.Output.Length;
                        return listing;
                    }
                }
                case "error":
                case "echo":
                {
                    string output = "";
                    foreach (var item in parameters)
                    {
                        if (item.Trim().StartsWith("\"") && item.EndsWith("\""))
                            output += item.Substring(1, item.Length - 2);
                        else
                            output += ExpressionEngine.Evaluate(item, PC);
                    }
                    Console.WriteLine((directive == "error" ? "User Error: " : "") + output);
                    return listing;
                }
                case "nolist":
                case "list": // TODO: Do either of these really matter?
                case "end":
                    return listing;
                case "fill":
                {
                    ulong amount = ExpressionEngine.Evaluate(parameters[0], PC);
                    if (parameters.Length == 1)
                    {
                        Array.Resize<string>(ref parameters, 2);
                        parameters[1] = "0";
                    }
                    listing.Output = new byte[amount];
                    for (int i = 0; i < (int)amount; i++)
                        listing.Output[i] = (byte)ExpressionEngine.Evaluate(parameters[1], PC++);
                    return listing;
                }
                case "option": // TODO: Spasm-style bitmap imports
                    return listing;
                case "org":
                    PC = (uint)ExpressionEngine.Evaluate(parameter, PC);
                    return listing;
                case "include":
                    {
                        string file = GetIncludeFile(parameter);
                        if (file == null)
                        {
                            listing.Error = AssemblyError.FileNotFound;
                            return listing;
                        }
                        FileNames.Push(parameter);
                        LineNumbers.Push(0);
                        string includedFile = File.ReadAllText(file) + "\n.endfile";
                        string[] lines = includedFile.Replace("\r", "").Split('\n');
                        Lines = Lines.Take(CurrentIndex + 1).Concat(lines).Concat(Lines.Skip(CurrentIndex + 1)).ToArray();
                        return listing;
                    }
                case "endfile": // Special directive
                    RootLineNumber--;
                    LineNumbers.Pop();
                    FileNames.Pop();
                    return null;
                case "equ":
                case "define":
                    // TODO: Macro
                    // TODO: Equates in a different way
                    ExpressionEngine.Equates.Add(parameters[0], (uint)ExpressionEngine.Evaluate(parameter.Substring(parameter.IndexOf(',') + 1).Trim(), PC));
                    return listing;
            }
            return null;
        }

        private string GetIncludeFile(string file)
        {
            file = file.Substring(1, file.Length - 2); // Remove <> or ""
            if (File.Exists(file))
                return file;
            foreach (var path in IncludePaths)
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
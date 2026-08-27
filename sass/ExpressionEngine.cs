﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace sasSX
{
	public class ExpressionEngine
	{
		public ExpressionEngine(AssemblySettings settings)
		{
			Symbols = new Dictionary<string, Symbol>();
			RelativeLabels = new List<RelativeLabel>();
			Settings = settings;
		}

		private enum Precedence
		{
			None = 		11,
			Unary = 	10,     // Not actually used.
			Power = 	9,      // We use ^ to mean exponentiation.
			Times = 	8,
			Div = 		7,
			Modulus = 	6,
			Plus = 		5,
			// Cesc
			And = 		4,
			Or = 		4,
			Not = 		4,
			RotateL = 	4,
			RotateR = 	4,
		}

		public ulong Evaluate(string expression, uint PC, int rootLineNumber)
		{
			string expre = expression.Replace (" ", "");
			if (expre == "0" || string.IsNullOrWhiteSpace(expre))
				return 0;

			if (expre.EndsWith ("_"))
				return (ulong)(long)relativeLabels (expre, rootLineNumber);

			// Cesc TODO, els operadors amb mes d'un caracter son problematics, es pot fer millor ?
			// Go through long so a negative result wraps (0xFF... for -1) instead of
			// saturating to 0: .NET Core clamps out-of-range double->ulong casts, the
			// old .NET Framework build wrapped them. Callers rely on the wrap for .db -1.
			return (ulong)(long) EvaluateSimple (expre.Replace("<<", "<").Replace(">>",">"));
		}

		// Evaluate the expression.
		public double EvaluateSimple(string expr)
		{
			int best_pos = 0;
			int parens = 0;

			int expr_len = expr.Length;
			//if (expr_len == 0) return 0;

			// If we find + or - now, then it's a unary operator.
			bool is_unary = true;

			// So far we have nothing.
			Precedence best_prec = Precedence.None;

			// Find the operator with the lowest precedence.
			// Look for places where there are no open
			// parentheses.
			for (int pos = 0; pos < expr_len; pos++)
			{
				// Examine the next character.
				char ch = expr[pos];

				// Assume we will not find an operator. In
				// that case, the next operator will not
				// be unary.
				bool next_unary = false;

				if (ch == '(')
				{
					// Increase the open parentheses count.
					parens += 1;

					// A + or - after "(" is unary.
					next_unary = true;
				}
				else if (ch == ')')
				{
					// Decrease the open parentheses count.
					parens -= 1;

					// An operator after ")" is not unary.
					next_unary = false;

					// If parens < 0, too many )'s.
					if (parens < 0)
						throw new FormatException("Too many close parentheses in '" + expr + "'");
				}
				else if (parens == 0)
				{
					// See if this is an operator.
					if ((ch == '^') || (ch == '*') ||
						(ch == '/') || (ch == '\\') ||
						(ch == '%') || (ch == '+') ||
						(ch == '-') || 
						// Cesc
						(ch == '&') || (ch == '|') ||
						(ch == '~') || (ch == '!') ||
						(ch == '<') || (ch == '>') 
					)
					{
						// An operator after an operator is unary.
						next_unary = true;

						// See if this operator has higher precedence than the current one.
						switch (ch)
						{
						case '^':
							if (best_prec >= Precedence.Power)
							{
								best_prec = Precedence.Power;
								best_pos = pos;
							}
							break;

						case '*':
						case '/':
							if (best_prec >= Precedence.Times)
							{
								best_prec = Precedence.Times;
								best_pos = pos;
							}
							break;

						case '%':
							if (best_prec >= Precedence.Modulus)
							{
								best_prec = Precedence.Modulus;
								best_pos = pos;
							}
							break;

						case '<':
							if (best_prec >= Precedence.RotateL)
							{
								best_prec = Precedence.RotateL;
								best_pos = pos;
							}
							break;

						case '>':
							if (best_prec >= Precedence.RotateR)
							{
								best_prec = Precedence.RotateR;
								best_pos = pos;
							}
							break;

						case '&':
							if (best_prec >= Precedence.And)
							{
								best_prec = Precedence.And;
								best_pos = pos;
							}
							break;

						case '|':
							if (best_prec >= Precedence.Or)
							{
								best_prec = Precedence.Or;
								best_pos = pos;
							}
							break;

						case '~':	// Cesc not
						case '+':
						case '-':
							// Ignore unary operators
							// for now.
							if ((!is_unary) &&
								best_prec >= Precedence.Plus)
							{
								best_prec = Precedence.Plus;
								best_pos = pos;
							}
							break;
						} 
					} 
				} 
				is_unary = next_unary;
			}

			// If the parentheses count is not zero, there's a ) missing.
			if (parens != 0)
			{
				throw new FormatException(
					"Missing close parenthesis in '" + expr + "'");
			}

			// Hopefully we have the operator.
			if (best_prec < Precedence.None)
			{
				string lexpr = expr.Substring(0, best_pos);
				string rexpr = expr.Substring(best_pos + 1);
				switch (expr[best_pos])
				{
				case '^':
					return Math.Pow(EvaluateSimple(lexpr), EvaluateSimple(rexpr));
				case '*':
					return
						EvaluateSimple(lexpr) * EvaluateSimple(rexpr);
				case '/':
					return
						EvaluateSimple(lexpr) / EvaluateSimple(rexpr);
				case '%':
					return
						EvaluateSimple(lexpr) % EvaluateSimple(rexpr);
				case '+':
					return
						EvaluateSimple(lexpr) + EvaluateSimple(rexpr);
				case '-':
					return
						EvaluateSimple(lexpr) - EvaluateSimple(rexpr);
				case '&':
					return
						(double)((int)EvaluateSimple(lexpr) & (int)EvaluateSimple(rexpr));
				case '|':
					return
						(double)((int)EvaluateSimple(lexpr) | (int)EvaluateSimple(rexpr));
				case '<':
					return
						(double)((int)EvaluateSimple(lexpr) << (int)EvaluateSimple(rexpr));
				case '>':
					return
						(double)((int)EvaluateSimple(lexpr) >> (int)EvaluateSimple(rexpr));					
				}
			}

			// if we do not yet have an operator, there
			// are several possibilities:
			//
			// 1. expr is (expr2) for some expr2.
			// 2. expr is -expr2 or +expr2 for some expr2.
			// 3. expr is Fun(expr2) for a function Fun.
			// 4. expr is a primitive.
			// 5. It's a literal like "3.14159".

			// Look for (expr2).
			if (expr.StartsWith("(") && expr.EndsWith(")"))
			{
				// Remove the parentheses.
				return EvaluateSimple(expr.Substring(1, expr_len - 2));
			}

			// Look for -expr2.
			if (expr.StartsWith("-"))
			{
				return -EvaluateSimple(expr.Substring(1));
			}

			// Look for +expr2.
			if (expr.StartsWith("+"))
			{
				return EvaluateSimple(expr.Substring(1));
			}

			// Look for ~expr2.
			if (expr.StartsWith("~"))
			{
				return (double)(~(int)EvaluateSimple(expr.Substring(1)));
			}

			// Look for Fun(expr2).
			if (expr_len > 5 && expr.EndsWith(")"))
			{
				// Find the first (.
				int paren_pos = expr.IndexOf("(");
				if (paren_pos > 0)
				{
					// See what the function is.
					string lexpr = expr.Substring(0, paren_pos);
					string rexpr = expr.Substring(paren_pos + 1, expr_len - paren_pos - 2);
					switch (lexpr.ToLower())
					{
					case "int":
						return (int)(EvaluateSimple(rexpr));
					}
				}
			}

			// See if it's a primitive.
			if (Symbols.ContainsKey(expr.ToLower()))
			{
				try
				{
					return Symbols[expr.ToLower()].Value;
				}
				catch (Exception)
				{
					throw new FormatException(
						"Primative '" + expr +"' has value '" + Symbols[expr] + "' which is not a Double.");
				}
			}

			// It must be a literal like "2.71828".

			return interpretValue(expr,0);
			/*
			try
			{
				return interpretValue(expr,0);
			}
			catch (Exception)
			{
				if (expr.Contains ('[') ||
					expr.Contains (']')|| 
					expr.Contains ("@@")) {
					Console.WriteLine ("Syntax error in {0}. Is for asmsx ? use the modifier --asmsx.", expr);
					return 0;
					//throw new FormatException ("Error evaluating '" + expr + "' as a constant." +
					//	"Error, is syntax is asmsx, use the modifier --asmsx.");
				} else {
					//throw new FormatException ("Error evaluating '" + expr + "' as a constant.");
					Console.WriteLine ("Error evaluating '" + expr + "' as a constant.");
					return 0;
				}
			}*/
		}

		/// <summary>
		/// Check for relative labels (special case, because they're bloody annoying to parse)
		/// </summary>
		/// <returns>The labels.</returns>
		/// <param name="expression">Expression.</param>
		/// <param name="rootLineNumber">Root line number.</param>
		double relativeLabels(string expression,int rootLineNumber)
		{			
			bool relative = true, firstPlus = false;
			int offset = 0;
			for (int i = 0; i < expression.Length - 1; i++) {
				if (expression [i] == '-')
					offset--;
				else if (expression [i] == '+') {
					if (firstPlus)
						offset++;
					else
						firstPlus = true;
				} else {
					relative = false;
					break;
				}
			}
			if (relative) {
				int i;
				for (i = 0; i < RelativeLabels.Count; i++) {
					if (RelativeLabels [i].RootLineNumber > rootLineNumber)
						break;
				}
				i += offset;
				if (i < 0 || i >= RelativeLabels.Count)
					throw new KeyNotFoundException ("Relative label not found.");
				return RelativeLabels [i].Address;
			}
			return 0d; // Cesc TODO excepcio no controlada ??
		}

		// Interpret value
		double interpretValue (string expression, uint PC)
		{
			// Interpret value
			if (expression == "$")
				return PC;
			else if (expression.StartsWith ("0x")) 
				// Hex
				return Convert.ToUInt64 (expression.Substring (2), 16);
			// Cesc floats
			else if (expression.Contains(".") && expression.Count (c => !"0123456789.".Contains (c)) == 0)
				// Try to convert the expression into a Double.
				return double.Parse(expression, CultureInfo.InvariantCulture);
			else if (expression.StartsWith ("$") || (expression.EndsWith ("h") &&
				expression.Remove (expression.Length - 1).ToLower ().Count (c => !"0123456789abcdef".Contains (c)) == 0))
				return Convert.ToUInt64 (expression.Trim ('$', 'h'), 16);
			else if (expression.StartsWith ("0b")) // Binary
				return Convert.ToUInt64 (expression.Substring (2), 2);
			else if (expression.StartsWith("%") || (expression.EndsWith("b") &&
				expression.Remove(expression.Length - 1).ToLower().Count(c => !"01".Contains(c)) == 0))
				return Convert.ToUInt64(expression.Trim('%', 'b'), 2);

			else if (expression.StartsWith("0o")) // Octal
				return Convert.ToUInt64(expression.Substring(2), 8);
			else if (expression == "true")
				return 1;
			else if (expression == "false")
				return 0;
			else if (expression.StartsWith("'") && expression.EndsWith("'"))
			{
				var character = expression.Substring(1, expression.Length - 2).Unescape();
				if (character.Length != 1)
					throw new InvalidOperationException("Invalid character.");
				return Settings.Encoding.GetBytes(character)[0];
			}
			else
			{
				// Check for number
				bool number = true;
				for (int i = 0; i < expression.Length; i++)
					if (!char.IsNumber (expression [i])) {
						number = false;
						break;
					}
				if (number) {
					if (expression == "") {
						expression = "0";
						return Convert.ToUInt64 (expression);
					} else
						return Convert.ToUInt64 (expression);
				} else {
					// Look up reference
					var symbol = expression.ToLower ();
					if (symbol.StartsWith ("."))
						symbol = symbol.Substring (1) + "@" + LastGlobalLabel;
					if (Symbols.ContainsKey (symbol))
						return Symbols [symbol].Value;
					throw new KeyNotFoundException ("The specified symbol:" + symbol + " was not found.");
				}
			}
		}

		public Dictionary<string, Symbol> Symbols { get; set; }
		public List<RelativeLabel> RelativeLabels { get; set; }
		public string LastGlobalLabel { get; set; }
		public AssemblySettings Settings { get; set; }
		// Grouped by priority, based on C operator precedence
		public static string[][] Operators = new[]
		{
			new[] { "*", "/", "%" },
			new[] { "+", "-" },
			new[] { "<", "<=", ">", ">=" },
			new[] { "<<", ">>" },
			new[] { "==", "!=" },
			new[] { "&" },
			new[] { "^" },
			new[] { "|" },
			new[] { "&&" },
			new[] { "||" }
		};

		/// <summary>
		/// The Z80 is a little-endian
		/// Converts from binary to little-endian byte[].
		/// </summary>
		/// <returns>The from binary.</returns>
		/// <param name="binary">Binary.</param>
		public static byte[] ConvertFromBinary(string binary)
		{
			while (binary.Length % 8 != 0)
				binary = "0" + binary;
			byte[] result = new byte[binary.Length / 8];
			int i = result.Length - 1;
			while (binary.Length > 0)
			{
				string octet = binary.Substring(binary.Length - 8);
				binary = binary.Remove(binary.Length - 8);
				result[i--] = Convert.ToByte(octet, 2);
			}
			return result;
		}

		/// <summary>
		/// Determines str is binary.
		/// </summary>
		/// <returns><c>true</c> if this instance is binary string the specified str; otherwise, <c>false</c>.</returns>
		/// <param name="str">String.</param>
		bool IsBinaryString(string str)
		{
			foreach (char c in str)
			{
				if (c < '0' || c > '1')
					return false;
			}
			return true;
		}
	}
}
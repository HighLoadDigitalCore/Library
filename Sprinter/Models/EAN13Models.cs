

using System;
using System.Text.RegularExpressions;

namespace Sprinter.Models
{
    public class EAN13
    {
        public static bool CheckCode(string code)
        {
            if (code == null || code.Length != 13)
                return false;

            int res;
            foreach (char c in code)
                if (!int.TryParse(c.ToString(), out res))
                    return false;

            char check = (char)('0' + CalculateChecksum(code.Substring(0, 12)));
            return code[12] == check;
        }
        public static int CalculateChecksum(string code)
        {
            if (code == null || code.Length != 12)
                throw new ArgumentException("Code length should be 12, i.e. excluding the checksum digit");

            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int v;
                if (!int.TryParse(code[i].ToString(), out v))
                    throw new ArgumentException("Invalid character encountered in specified code.");
                sum += (i % 2 == 0 ? v : v * 3);
            }
            int check = 10 - (sum % 10);
            return check % 10;
        }

        public static string ClearIsbn(string isbn)
        {
            try
            {
                Regex incorrect = new Regex(@"([^0-9xX])");
                isbn = incorrect.Replace(isbn, "-");
                isbn =
                    isbn.Replace("--", "-").Replace("--", "-").Replace("--", "-").Replace("--", "-").Replace("--", "-").
                        Replace("--", "-").Replace("--", "-").Replace("--", "-").Replace(" ", "-").Replace("x", "X").
                        Trim();
                if (isbn.StartsWith("-")) isbn = isbn.Substring(1);
                if (isbn.EndsWith("-")) isbn = isbn.Substring(0, isbn.Length - 1);
                return isbn;
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static string NormalizeIsbn(string isbn)
        {
            return ClearIsbn(isbn).Replace("-", "");
        }

        public static bool CheckIsbn(string isbn)
        {
            if (isbn == null)
                return false;

            isbn = NormalizeIsbn(isbn);
            if (isbn.Length != 10)
                return false;

            int res;
            for (int i = 0; i < 9; i++)
                if (!int.TryParse(isbn[i].ToString(), out res))
                    return false;

            int sum = 0;
            for (int i = 0; i < 9; i++)
                sum += (i + 1) * int.Parse(isbn[i].ToString());

            int r = sum % 11;
            if (r == 10)
                return isbn[9] == 'X';
            else
                return isbn[9] == (char)('0' + r);
        }

        public static string IsbnToEan13(string isbn)
        {
            try
            {
                var tmp = isbn.Replace("-", "");
                if(isbn.StartsWith("978") || isbn.StartsWith("979"))
                    tmp = isbn.Substring(3);

                if (tmp.Length == 8 || tmp.Length == 7) //ISSN
                {
                    isbn = NormalizeIsbn(isbn);
                    if (!isbn.StartsWith("977"))
                        isbn = "977-" + isbn;

                    isbn = isbn.Replace("-", "");
                    string code = isbn.Substring(0, 10) + "00";
                    code += (char) ('0' + CalculateChecksum(code));
                    return code;
                }
                else
                {
                    isbn = NormalizeIsbn(isbn);
                    if (!isbn.StartsWith("978") && !isbn.StartsWith("979"))
                        isbn = "978-" + isbn;
                    isbn = isbn.Replace("-", "");
                    string code = isbn.Substring(0, 12);
                    code += (char) ('0' + CalculateChecksum(code));
                    return code;
                }
            }
            catch
            {
                return "0";
            }
        }
    }
}
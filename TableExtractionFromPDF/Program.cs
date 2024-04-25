using iText.Kernel.Pdf;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace TableExtractionFromPDF
{
    class Program
    {
        static void Main(string[] args)
        {
            PdfReader reader = new PdfReader(AppDomain.CurrentDomain.BaseDirectory + @"TableTest.pdf");
            
            PdfDocument document = new PdfDocument(reader);
            PdfPage page = document.GetPage(2);
            FilterTableEventListener renderListener = new FilterTableEventListener(page, true);
            System.Data.DataSet ds = renderListener.GetTables();
            WriteFile(ds.Tables[0], AppDomain.CurrentDomain.BaseDirectory + "result.txt");
            document.Close();
            reader.Close();
        }
        static void WriteFile(DataTable dt, string outputFilePath)
        {
            int[] maxLengths = new int[dt.Columns.Count];

            for (int i = 0; i < dt.Columns.Count; i++)
            {
                maxLengths[i] = dt.Columns[i].ColumnName.Length;

                foreach (DataRow row in dt.Rows)
                {
                    if (!row.IsNull(i))
                    {
                        int length = row[i].ToString().Length;

                        if (length > maxLengths[i])
                        {
                            maxLengths[i] = length;
                        }
                    }
                }
            }

            using (StreamWriter sw = new StreamWriter(outputFilePath, false))
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    sw.Write(dt.Columns[i].ColumnName.PadRight(maxLengths[i] + 2));
                }

                sw.WriteLine();

                foreach (DataRow row in dt.Rows)
                {
                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        if (!row.IsNull(i))
                        {
                            sw.Write(row[i].ToString().PadRight(maxLengths[i] + 2));
                        }
                        else
                        {
                            sw.Write(new string(' ', maxLengths[i] + 2));
                        }
                    }

                    sw.WriteLine();
                }

                sw.Close();
            }
        }
    }
}

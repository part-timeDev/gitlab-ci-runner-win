using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace gitlab_ci_runner.helper
{
    class OutputParser
    {
        public static StringBuilder prettify(string log, bool shrinkOutput, bool printTestResults)
        {
            StringComparison sco = StringComparison.CurrentCultureIgnoreCase;

            StringBuilder returnValue = new StringBuilder();
            bool NextLines = false;

            int success = 0;
            int failed = 0;
            int ignored = 0;

            string successpattern = @"Test:? .*? ==>? OK";
            string failurepattern = @"Test:? .*? ==>? failed";
            string ignoredpattern = @"Test:? .*? ==>? ignored";
            string pattern = @"Test:? .*? ==>? (OK|failed|ignored)";

            if (!String.IsNullOrEmpty(log))
            {
                foreach (string line in log.Split('\n'))
                {
                    if (shrinkOutput)
                    {
                        if (line.StartsWith("git ", sco)
                        || line.StartsWith("--- ")
                        || line.StartsWith("Starting Target:", sco)
                        || line.StartsWith("Starting FinalTarget:", sco)
                        || line.StartsWith("---------------------------------------------------------------------"))
                            NextLines = true;

                        if (NextLines)
                            returnValue.Append(line).Append("\n");

                        if (line.StartsWith("HEAD ", sco)
                            || line.StartsWith("Finished Target", sco)
                            || line.StartsWith("------ Target Test ------", sco)
                            || line.StartsWith("--- enter runTestTargetBody() ---", sco)
                            || line.StartsWith("Starting Target: FixLineBreaks", sco)
                            || line.StartsWith("Starting FinalTarget:", sco))
                            NextLines = false;
                    }
                    else
                        returnValue.Append(line).Append("\n");

                    if (printTestResults)
                    {
                        if (Regex.IsMatch(line, pattern))
                        {
                            MatchCollection mc = Regex.Matches(line, pattern);

                            foreach (Match part in mc)
                            {
                                if (Regex.IsMatch(part.ToString(), successpattern))
                                    success++;
                                else if (Regex.IsMatch(part.ToString(), failurepattern))
                                    failed++;
                                else if (Regex.IsMatch(part.ToString(), ignoredpattern))
                                    ignored++;
                            }
                        }
                    }
                }
            }
            if (printTestResults)
            {
                returnValue.Append("\n");
                returnValue.Append(String.Format("---------------------------------------------------------------------\n"));
                returnValue.Append(String.Format("Test results: {0} total, {1} success, {2} failed, {3} ignored\n", (success + failed + ignored), success, failed, ignored));
                returnValue.Append(String.Format("---------------------------------------------------------------------\n"));
            }
            return returnValue;
        }
    }
}

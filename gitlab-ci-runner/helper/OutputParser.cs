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
            StringBuilder failedTests = new StringBuilder();
            bool NextLines = false;
            bool runningTests = false;

            int success = 0;
            int failed = 0;
            int ignored = 0;

            string successpattern = "OK";
            string failurepattern = "failed";
            string ignoredpattern = "ignored";
            string testresultpattern = @"Test: (.*? )(==>)? ?(" + successpattern + "|" + failurepattern + "|" + ignoredpattern + ")";
            int testresult = 3;

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

                        if (line.StartsWith("--- enter runTestTargetBody() ---", sco))
                            runningTests = true;
                    }
                    else
                        returnValue.Append(line).Append("\n");

                    if (printTestResults || runningTests)
                    {
                        MatchCollection mc = Regex.Matches(line, testresultpattern);
                        if (mc.Count > 0)
                        {
                            foreach (Match part in mc)
                            {
                                if (part.Groups[testresult].Value.Equals(successpattern, sco))
                                    success++;
                                else if (part.Groups[testresult].Value.Equals(failurepattern, sco))
                                {
                                    failedTests.Append(String.Format("Test {0}==> {1}", part.Groups[1].Value, failurepattern)).Append("\n");
                                    failed++;
                                }
                                else if (part.Groups[testresult].Value.Equals(ignoredpattern, sco))
                                    ignored++;
                            }
                        }
                    }
                }
            }

            if (printTestResults || runningTests)
            {
                string state = "progress";

                if (!shrinkOutput)
                    state = "results";

                returnValue.Append("\n");
                if (failedTests.Length > 0)
                {
                    returnValue.Append(String.Format("--- Failed Tests ----------------------------------------------------\n"));
                    returnValue.Append(failedTests);
                }
                returnValue.Append(String.Format("---------------------------------------------------------------------\n"));
                returnValue.Append(String.Format("Test {0}: {1} total, {2} success, {3} failed, {4} ignored\n", state, (success + failed + ignored), success, failed, ignored));
                returnValue.Append(String.Format("---------------------------------------------------------------------\n"));
            }

            if (shrinkOutput)
                returnValue.Append("NOTE: detailed output is shown after the build finished.").Append("\n");

            return returnValue;
        }
    }
}

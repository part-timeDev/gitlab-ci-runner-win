using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace gitlab_ci_runner.helper
{
    class Command
    {
        List<string> steps = new List<string>();

        public Command()
        {

        }

        public Command(string step)
        {
            if (!String.IsNullOrEmpty(step.Trim()))
                steps.Add(step.Trim());
        }

        public Command Add(string step)
        {
            if (!String.IsNullOrEmpty(step.Trim()))
                steps.Add(step.Trim());

            return this;
        }

        public override string ToString()
        {
            return String.Join(" && ", steps);
        }
    }
}

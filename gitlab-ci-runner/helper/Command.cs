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
            steps.Add(step);
        }

        public Command Add(string step)
        {
            steps.Add(step);
            return this;
        }

        public override string ToString()
        {
            List<string> finalsteps = new List<string>();
            foreach (string part in this.steps)
            {
                if (String.IsNullOrEmpty(part.Trim()))
                    continue;

                finalsteps.Add(part.Trim());
            }
            return String.Join(" && ", finalsteps);
        }
    }
}

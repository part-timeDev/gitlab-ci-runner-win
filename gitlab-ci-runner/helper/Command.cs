using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace gitlab_ci_runner.helper
{
    class Command
    {
        List<string> steps = new List<string>();

        public Command Add(string step)
        {
            steps.Add(step);
            return this;
        }

        public override string ToString()
        {
            return String.Join(" && ", steps);
        }
    }
}

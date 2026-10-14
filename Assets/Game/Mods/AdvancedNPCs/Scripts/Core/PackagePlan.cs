using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    public class PackageStep
    {
        public string Package;
        public Dictionary<string,object> Parameters = new Dictionary<string,object>();
        public double Duration = -1;
    }
    public class PlannedStep
    {
        public string Package;
        public PackageSettings Settings;
        public double Duration;
    }
    public class PackagePlan
    {
        public bool Sequence, Loop;
        public readonly List<PlannedStep> Steps = new List<PlannedStep>();
        public static PackagePlan Build(AiPackage package, Dictionary<string,object> overrides, Func<string,AiPackage> find, out string error)
        {
            var settings = PackageParser.Resolve(package,overrides,out error);
            if (settings == null) return null;
            var plan = new PackagePlan {Sequence=settings.Type=="sequence",Loop=settings.Loop};
            if (!plan.Sequence) plan.Steps.Add(new PlannedStep {Package=package.Id,Settings=settings,Duration=-1});
            else if (find == null) { error = "sequence needs a package lookup"; return null; }
            else
                foreach (var step in settings.Steps)
                {
                    var child = PackageParser.Resolve(find(step.Package),step.Parameters,out error);
                    if (child == null) { error = "step " + (plan.Steps.Count+1) + " ("+step.Package+"): "+error; return null; }
                    if (child.Type == "sequence") { error = "sequence steps must reference leaf packages; nesting/cycles are unsupported"; return null; }
                    if (step.Duration < 0 && child.Type != "travel_to")
                    { error = "step " + (plan.Steps.Count+1) + " needs duration; only travel_to can finish automatically on arrival"; return null; }
                    plan.Steps.Add(new PlannedStep {Package=step.Package,Settings=child,Duration=step.Duration});
                }
            return plan;
        }
    }
}

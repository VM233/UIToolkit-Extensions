using System;

namespace VM233.UIElements
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public sealed class UiTransitionDriverAttribute : Attribute
    {
        public string PhaseMember { get; }
        public string RetirementMember { get; }

        public UiTransitionDriverAttribute(string phaseMember, string retirementMember)
        {
            PhaseMember = phaseMember; RetirementMember = retirementMember;
        }
    }
}

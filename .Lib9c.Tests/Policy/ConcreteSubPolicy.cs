namespace Lib9c.Tests.Policy
{
    using Nekoyume.Blockchain.Policy;

    public sealed class ConcreteSubPolicy<T> : VariableSubPolicy<T>
    {
        private ConcreteSubPolicy(T defaultValue)
            : base(defaultValue)
        {
        }

        private ConcreteSubPolicy(
            ConcreteSubPolicy<T> concretePolicy,
            SpannedSubPolicy<T> spannedSubPolicy)
            : base(concretePolicy, spannedSubPolicy)
        {
        }

        public static ConcreteSubPolicy<T> Create(T defaultValue)
        {
            return new ConcreteSubPolicy<T>(defaultValue);
        }
    }
}

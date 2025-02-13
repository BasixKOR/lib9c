namespace Lib9c.Tests.Policy
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Nekoyume.Blockchain.Policy;
    using Xunit;

    public class ConcreteSubPolicyTest
    {
        public ConcreteSubPolicyTest()
        {
        }

        [Fact]
        public void Constructor()
        {
            IVariableSubPolicy<bool> genericSubPolicy;
            var indices = Enumerable.Range(0, 100).Select(i => (long)i).ToList();

            var first = new SpannedSubPolicy<bool>(10, null, null, true);
            var badSecond = new SpannedSubPolicy<bool>(5, null, index => index % 2 == 0, true);
            var second = new SpannedSubPolicy<bool>(20, 50, index => index % 2 == 0, true);
            var third = new SpannedSubPolicy<bool>(30, 40, null, true);
            var fourth = new SpannedSubPolicy<bool>(50, 80, index => index % 5 == 0, true);

            // Should be fine.
            genericSubPolicy = ConcreteSubPolicy<bool>
                .Create(false)
                // 10 ~ 19 => count 10
                .Add(first)
                // 20 ~ 29 && mod 2 => count 5
                .Add(second)
                // 30 ~ 40 => count 11
                .Add(third)
                // 50 ~ 80 && mod 5 => count 7
                .Add(fourth);

            Assert.Equal(4, genericSubPolicy.SpannedSubPolicies.Count);
            Assert.Equal(10, genericSubPolicy.SpannedSubPolicies[0].StartIndex);
            Assert.Equal(19, genericSubPolicy.SpannedSubPolicies[0].EndIndex);
            Assert.Equal(20, genericSubPolicy.SpannedSubPolicies[1].StartIndex);
            Assert.Equal(29, genericSubPolicy.SpannedSubPolicies[1].EndIndex);
            Assert.Equal(30, genericSubPolicy.SpannedSubPolicies[2].StartIndex);
            Assert.Equal(40, genericSubPolicy.SpannedSubPolicies[2].EndIndex);
            Assert.Equal(50, genericSubPolicy.SpannedSubPolicies[3].StartIndex);
            Assert.Equal(80, genericSubPolicy.SpannedSubPolicies[3].EndIndex);

            Assert.Equal(
                indices.Where(i => genericSubPolicy.Getter(i)).Count(),
                genericSubPolicy.SpannedSubPolicies
                    .Select(s => indices.Where(i => s.IsTargetIndex(i)).Count())
                    .Sum());
            Assert.Equal(33, indices.Where(i => genericSubPolicy.Getter(i)).Count());
            // Check first one is no longer indefinite and cut short.
            Assert.True(first.Indefinite);
            Assert.False(genericSubPolicy.SpannedSubPolicies.First().Indefinite);
            Assert.Equal(19, genericSubPolicy.SpannedSubPolicies.First().EndIndex);

            // Out of order addition should not work.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ConcreteSubPolicy<bool>
                    .Create(false)
                    .Add(fourth)
                    .Add(third));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ConcreteSubPolicy<bool>
                    .Create(false)
                    .Add(first)
                    .Add(badSecond));

            // Create using AddRange().
            genericSubPolicy = ConcreteSubPolicy<bool>
                .Create(false)
                .AddRange(new List<SpannedSubPolicy<bool>>() { first, second, third, fourth, }.ToImmutableList());
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ConcreteSubPolicy<bool>
                    .Create(false)
                    .AddRange(new List<SpannedSubPolicy<bool>>() { second, first, }.ToImmutableList()));

            // Type check
            Assert.IsType<ConcreteSubPolicy<bool>>(genericSubPolicy);
        }
    }
}

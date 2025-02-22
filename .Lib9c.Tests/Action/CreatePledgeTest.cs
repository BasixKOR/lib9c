namespace Lib9c.Tests.Action
{
    using System;
    using System.Collections.Generic;
    using Libplanet;
    using Libplanet.Crypto;
    using Libplanet.State;
    using Nekoyume;
    using Nekoyume.Action;
    using Nekoyume.Model.State;
    using Serilog;
    using Xunit;
    using Xunit.Abstractions;

    public class CreatePledgeTest
    {
        public CreatePledgeTest(ITestOutputHelper outputHelper)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(outputHelper)
                .CreateLogger();
        }

        [Theory]
        [InlineData(true, null)]
        [InlineData(false, typeof(PermissionDeniedException))]
        public void Execute(bool admin, Type exc)
        {
            var adminAddress = new PrivateKey().ToAddress();
            var poolAddress = new PrivateKey().ToAddress();
            var adminState = new AdminState(adminAddress, 150L);
            var patronAddress = new PrivateKey().ToAddress();
            var mead = Currencies.Mead;
            var agentAddress = new PrivateKey().ToAddress();
            var pledgeAddress = agentAddress.GetPledgeAddress();
            IAccountStateDelta states = new State()
                .SetState(Addresses.Admin, adminState.Serialize())
                .MintAsset(patronAddress, 4 * 500 * mead);

            var agentAddresses = new List<(Address, Address)>
            {
                (agentAddress, pledgeAddress),
            };
            for (int i = 0; i < 499; i++)
            {
                var address = new PrivateKey().ToAddress();
                agentAddresses.Add((address, address.GetPledgeAddress()));
            }

            var action = new CreatePledge
            {
                PatronAddress = patronAddress,
                Mead = RequestPledge.DefaultRefillMead,
                AgentAddresses = agentAddresses,
            };

            Address singer = admin ? adminAddress : poolAddress;
            var actionContext = new ActionContext
            {
                Signer = singer,
                PreviousStates = states,
            };

            if (exc is null)
            {
                var nextState = action.Execute(actionContext);

                Assert.Equal(0 * mead, nextState.GetBalance(patronAddress, mead));
                Assert.Equal(4 * mead, nextState.GetBalance(agentAddress, mead));
            }
            else
            {
                Assert.Throws(exc, () => action.Execute(actionContext));
            }
        }
    }
}

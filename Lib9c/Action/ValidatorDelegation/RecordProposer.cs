using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Nekoyume.Action.Guild.Migration.LegacyModels;
using Nekoyume.Extensions;
using Nekoyume.ValidatorDelegation;
using static Nekoyume.Model.WorldInformation;

namespace Nekoyume.Action.ValidatorDelegation
{
    /// <summary>
    /// An action for recording proposer of the block to use in next block's reward distribution.
    /// </summary>
    public sealed class RecordProposer : ActionBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="RecordProposer"/>.
        /// </summary>
        public RecordProposer()
        {
        }

        /// <inheritdoc cref="IAction.PlainValue"/>
        public override IValue PlainValue => new Bencodex.Types.Boolean(true);

        /// <inheritdoc cref="IAction.LoadPlainValue(IValue)"/>
        public override void LoadPlainValue(IValue plainValue)
        {
            // Method intentionally left empty.
        }

        /// <inheritdoc cref="IAction.Execute(IActionContext)"/>
        public override IWorld Execute(IActionContext context)
        {
            var world = context.PreviousState;

            if (world.GetDelegationMigrationHeight() is null)
            {
                return world;
            }

            return world.MutateAccount(
                Addresses.ValidatorList,
                account => account.SetState(
                    ProposerInfo.Address,
                    new ProposerInfo(context.BlockIndex, context.Miner).Bencoded));
        }
    }
}

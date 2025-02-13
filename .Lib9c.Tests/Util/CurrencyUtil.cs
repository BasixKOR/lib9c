namespace Lib9c.Tests.Util
{
    using Libplanet.Action;
    using Libplanet.Action.State;
    using Libplanet.Crypto;
    using Libplanet.Types.Assets;
    using Nekoyume.Module;

    public static class CurrencyUtil
    {
        public static IWorld AddCurrency(
            IActionContext context,
            IWorld state,
            Address agentAddress,
            Currency currency,
            FungibleAssetValue amount
        )
        {
            return state.MintAsset(context, agentAddress, amount);
        }
    }
}

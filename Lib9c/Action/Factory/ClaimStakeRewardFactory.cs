using System;
using Libplanet;

namespace Nekoyume.Action.Factory
{
    public static class ClaimStakeRewardFactory
    {
        // NOTE: This method does not return a type of `ClaimStakeReward1`.
        //       Because it is not obsoleted yet.
        public static IClaimStakeReward CreateByBlockIndex(
            long blockIndex,
            Address avatarAddress)
        {
            if (blockIndex > ClaimStakeReward2.ObsoletedIndex)
            {
                return new ClaimStakeReward(avatarAddress);
            }

            // FIXME: This method should consider the starting block index of
            //        `claim_stake_reward2`. And if the `blockIndex` is less than
            //        the starting block index, it should throw an exception.
            // default: Version 2
            return new ClaimStakeReward2(avatarAddress);
        }

        public static IClaimStakeReward CreateByVersion(
            int version,
            Address avatarAddress) => version switch
        {
            1 => new ClaimStakeReward1(avatarAddress),
            2 => new ClaimStakeReward2(avatarAddress),
            3 => new ClaimStakeReward(avatarAddress),
            _ => throw new ArgumentOutOfRangeException(
                $"Invalid version: {version}"),
        };
    }
}

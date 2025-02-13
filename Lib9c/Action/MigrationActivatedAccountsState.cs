using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Bencodex.Types;
using Lib9c.Abstractions;
using Libplanet.Action;
using Libplanet.Action.State;
using Nekoyume.Model;
using Nekoyume.Model.State;
using Nekoyume.Module;
using Serilog;

namespace Nekoyume.Action
{
    [Serializable]
    [ActionType("migration_activated_accounts_state")]
    public class MigrationActivatedAccountsState : GameAction, IMigrationActivatedAccountsStateV1
    {
        public override IWorld Execute(IActionContext context)
        {
            GasTracer.UseGas(1);
            var states = context.PreviousState;

            CheckPermission(context);

            Log.Debug($"Start {nameof(MigrationActivatedAccountsState)}");
            if (states.TryGetLegacyState(Nekoyume.Addresses.ActivatedAccount, out Dictionary rawState))
            {
                var activatedAccountsState = new ActivatedAccountsState(rawState);
                var accounts = activatedAccountsState.Accounts;
                foreach (var agentAddress in accounts)
                {
                    var address = agentAddress.Derive(ActivationKey.DeriveKey);
                    if (states.GetLegacyState(address) is null)
                    {
                        states = states.SetLegacyState(address, true.Serialize());
                    }
                }
                Log.Debug($"Finish {nameof(MigrationActivatedAccountsState)}");
                return states;
            }

            throw new ActivatedAccountsDoesNotExistsException();
        }

        protected override IImmutableDictionary<string, IValue> PlainValueInternal => new Dictionary<string, IValue>
        {
        }.ToImmutableDictionary();

        protected override void LoadPlainValueInternal(IImmutableDictionary<string, IValue> plainValue)
        {
        }
    }
}

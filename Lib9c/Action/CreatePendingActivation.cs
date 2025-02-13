using System;
using System.Collections.Generic;
using Bencodex.Types;
using Lib9c.Abstractions;
using Libplanet.Action;
using Libplanet.Action.State;
using Nekoyume.Model.State;
using Nekoyume.Module;

namespace Nekoyume.Action
{
    /// <summary>
    /// Introduced at Initial commit(2e645be18a4e2caea031c347f00777fbad5dbcc6)
    /// Updated at https://github.com/planetarium/lib9c/pull/957
    /// </summary>
    [Serializable]
    [ActionType("create_pending_activation")]
    [ActionObsolete(ActionObsoleteConfig.V200030ObsoleteIndex)]
    public class CreatePendingActivation : ActionBase, ICreatePendingActivationV1
    {
        public PendingActivationState PendingActivation { get; private set; }

        IValue ICreatePendingActivationV1.PendingActivation => PendingActivation.Serialize();

        public override IValue PlainValue => Dictionary.Empty
            .Add("type_id", "create_pending_activation")
            .Add("values", new Dictionary(
                new[]
                {
                    new KeyValuePair<IKey, IValue>(
                        (Text)"pending_activation",
                        PendingActivation.Serialize()
                    ),
                }
            ));

        public CreatePendingActivation()
        {
        }

        public CreatePendingActivation(PendingActivationState activationKey)
        {
            PendingActivation = activationKey;
        }

        public override IWorld Execute(IActionContext context)
        {
            GasTracer.UseGas(1);

            CheckObsolete(ActionObsoleteConfig.V200030ObsoleteIndex, context);
            CheckPermission(context);

            return context.PreviousState.SetLegacyState(
                PendingActivation.address,
                PendingActivation.Serialize()
            );
        }

        public override void LoadPlainValue(IValue plainValue)
        {
            var asDict = (Dictionary)((Dictionary)plainValue)["values"];
            PendingActivation = new PendingActivationState((Dictionary) asDict["pending_activation"]);
        }
    }
}

using Network;

namespace Oxide.Plugins
{
    [Info("No Drone Sway", "WhiteThunder", "1.0.1")]
    [Description("Drones no longer sway in the wind, if they have attachments.")]
    internal class NoDroneSway : CovalencePlugin
    {
        #region Fields

        private const int DroneThrottleUpFlag = (int)BaseEntity.Flags.Reserved1;
        private const int DroneFlyingFlag = (int)BaseEntity.Flags.Reserved2;
        private readonly object False = false;

        #endregion

        #region Hooks

        private void OnEntitySaved(Drone drone, BaseNetworkable.SaveInfo saveInfo)
        {
            if (ShouldSway(drone))
                return;

            saveInfo.msg.baseEntity.flags = ModifyDroneFlags(drone);
        }

        private object OnEntityFlagsNetworkUpdate(Drone drone)
        {
            if (ShouldSway(drone))
                return null;

            var subscribers = drone.GetSubscribers();
            if (subscribers != null && subscribers.Count > 0)
            {
                var write = Net.sv.StartWrite();
                write.PacketID(Message.Type.EntityFlags);
                write.EntityID(drone.net.ID);
                write.Int32(ModifyDroneFlags(drone));
                write.Send(new SendInfo(subscribers));
            }

            drone.gameObject.SendOnSendNetworkUpdate(drone);
            return False;
        }

        #endregion

        #region Helpers

        private static int ModifyDroneFlags(Drone drone)
        {
            var flags = (int)drone.flags;

            if ((flags & DroneFlyingFlag) != 0)
            {
                flags = flags & ~DroneFlyingFlag | DroneThrottleUpFlag;
            }

            return flags;
        }

        private static bool ShouldSway(Drone drone)
        {
            // Drones with attachments should not sway.
            if (drone.children.Count > 0)
            {
                for (var i = 0; i < drone.children.Count; i++)
                {
                    var sphereChild = drone.children[i] as SphereEntity;
                    if ((object)sphereChild == null)
                        return false;

                    for (var j = 0; j < sphereChild.children.Count; j++)
                    {
                        var grandChild = sphereChild.children[j];

                        // Resized search lights are permitted (Drone Lights).
                        if (grandChild is SearchLight)
                            continue;

                        return false;
                    }
                }
            }

            // Drones with a re-parented rigid body are probably resized and should not sway.
            if (drone.body.gameObject != drone.gameObject)
                return false;

            return true;
        }

        #endregion
    }
}

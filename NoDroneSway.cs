using Network;

namespace Oxide.Plugins
{
    [Info("No Drone Sway", "WhiteThunder", "1.0.0")]
    [Description("Drones no longer sway in the wind, if they have attachments.")]
    internal class NoDroneSway : CovalencePlugin
    {
        #region Fields

        private const BaseEntity.Flags DroneFlyingFlag = BaseEntity.Flags.Reserved2;
        private readonly object False = false;

        #endregion

        #region Hooks

        private void OnEntitySaved(Drone drone, BaseNetworkable.SaveInfo saveInfo)
        {
            if (ShouldSway(drone))
                return;

            saveInfo.msg.baseEntity.flags = RemoveDroneFlyingFlag(saveInfo.msg.baseEntity.flags);
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
                write.Int32(RemoveDroneFlyingFlag((int)drone.flags));
                write.Send(new SendInfo(subscribers));
            }

            drone.gameObject.SendOnSendNetworkUpdate(drone);
            return False;
        }

        #endregion

        #region Helpers

        private static int RemoveDroneFlyingFlag(int flags)
        {
            return flags & ~(int)DroneFlyingFlag;
        }

        private static bool ShouldSway(Drone drone)
        {
            // Drones with attachments should not sway.
            if (drone.children.Count > 0)
            {
                for (var i = 0; i < drone.children.Count; i++)
                {
                    // Resized child entities are permitted (intended for Drone Lights).
                    if (!(drone.children[i] is SphereEntity))
                        return false;
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

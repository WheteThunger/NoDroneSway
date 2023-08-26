using Network;

namespace Oxide.Plugins
{
    [Info("No Drone Sway", "WhiteThunder", "1.0.2")]
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

            // Don't change flags for the controller because that would prevent viewing the pitch.
            // This approach is possible because network caching is disabled for RemoteControlEntity.
            var controllerSteamId = drone.ControllingViewerId?.SteamId ?? 0;
            if (controllerSteamId == 0 || controllerSteamId != (saveInfo.forConnection?.ownerid ?? 0))
            {
                saveInfo.msg.baseEntity.flags = ModifyDroneFlags(drone);
            }
        }

        private object OnEntityFlagsNetworkUpdate(Drone drone)
        {
            if (ShouldSway(drone))
                return null;

            var subscribers = drone.GetSubscribers();
            if (subscribers != null && subscribers.Count > 0)
            {
                var controllerSteamId = drone.ControllingViewerId?.SteamId ?? 0;
                if (controllerSteamId == 0)
                {
                    SendFlagsUpdate(drone, ModifyDroneFlags(drone), new SendInfo(subscribers));
                }
                else
                {
                    var otherConnections = Facepunch.Pool.GetList<Connection>();

                    Connection controllerConnection = null;
                    foreach (var connection in subscribers)
                    {
                        if (connection.ownerid == controllerSteamId)
                        {
                            controllerConnection = connection;
                        }
                        else
                        {
                            otherConnections.Add(connection);
                        }
                    }

                    var flags = ModifyDroneFlags(drone);

                    if (otherConnections.Count > 0)
                    {
                        SendFlagsUpdate(drone, flags, new SendInfo(otherConnections));
                    }

                    if (controllerConnection != null)
                    {
                        SendFlagsUpdate(drone, flags, new SendInfo(controllerConnection));
                    }

                    Facepunch.Pool.FreeList(ref otherConnections);
                }
            }

            drone.gameObject.SendOnSendNetworkUpdate(drone);
            return False;
        }

        #endregion

        #region Helpers

        private static void SendFlagsUpdate(BaseEntity entity, int flags, SendInfo sendInfo)
        {
            var write = Net.sv.StartWrite();
            write.PacketID(Message.Type.EntityFlags);
            write.EntityID(entity.net.ID);
            write.Int32(flags);
            write.Send(sendInfo);
        }

        private static int ModifyDroneFlags(BaseEntity drone)
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
            if (drone != null && drone.body.gameObject != drone.gameObject)
                return false;

            return true;
        }

        #endregion
    }
}

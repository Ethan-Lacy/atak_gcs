using System;
using System.Collections.Generic;

namespace AgentManagerPlugin.Services
{
    /// <summary>
    /// Manages multiple drone MAVLink connections
    /// </summary>
    public class DroneManager
    {
        private static DroneManager _instance;
        private Dictionary<string, MavlinkAgent> _drones;

        public static DroneManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new DroneManager();
                return _instance;
            }
        }

        private DroneManager()
        {
            _drones = new Dictionary<string, MavlinkAgent>();
        }

        public Dictionary<string, MavlinkAgent> GetAllDrones()
        {
            return _drones;
        }

        public MavlinkAgent GetDrone(string droneId)
        {
            return _drones.ContainsKey(droneId) ? _drones[droneId] : null;
        }

        public void AddDrone(string droneId, MavlinkAgent drone)
        {
            _drones[droneId] = drone;
        }

        public void RemoveDrone(string droneId)
        {
            if (_drones.ContainsKey(droneId))
            {
                _drones[droneId].Dispose();
                _drones.Remove(droneId);
            }
        }

        public List<string> GetDroneIds()
        {
            return new List<string>(_drones.Keys);
        }
    }
}

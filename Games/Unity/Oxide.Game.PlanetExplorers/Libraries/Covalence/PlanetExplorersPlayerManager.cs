﻿using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Game.PlanetExplorers.Libraries.Covalence
{
    /// <summary>
    /// Represents a generic player manager
    /// </summary>
    public class PlanetExplorersPlayerManager : IPlayerManager
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
        private struct PlayerRecord
        {
            public string Name;
            public ulong Id;
        }

        private readonly IDictionary<string, PlayerRecord> playerData;
        private readonly IDictionary<string, PlanetExplorersPlayer> allPlayers;
        private readonly IDictionary<string, PlanetExplorersPlayer> connectedPlayers;

        internal PlanetExplorersPlayerManager()
        {
            Core.Utility.DatafileToProto<Dictionary<string, PlayerRecord>>("oxide.covalence");
            playerData = ProtoStorage.Load<Dictionary<string, PlayerRecord>>("oxide.covalence") ?? new Dictionary<string, PlayerRecord>();
            allPlayers = new Dictionary<string, PlanetExplorersPlayer>();
            connectedPlayers = new Dictionary<string, PlanetExplorersPlayer>();

            foreach (var pair in playerData) allPlayers.Add(pair.Key, new PlanetExplorersPlayer(pair.Value.Id, pair.Value.Name));
        }

        private void NotifyPlayerJoin(Player player)
        {
            var id = player.steamId.ToString();

            PlayerRecord record;
            if (playerData.TryGetValue(id, out record))
            {
                record.Name = player.roleName;
                playerData[id] = record;
                allPlayers.Remove(id);
                allPlayers.Add(id, new PlanetExplorersPlayer(player));
            }
            else
            {
                record = new PlayerRecord {Id = player.steamId, Name = player.roleName };
                playerData.Add(id, record);
                allPlayers.Add(id, new PlanetExplorersPlayer(player));
            }

            ProtoStorage.Save(playerData, "oxide.covalence");
        }

        internal void NotifyPlayerConnect(Player player) => connectedPlayers[player.steamId.ToString()] = new PlanetExplorersPlayer(player);

        internal void NotifyPlayerDisconnect(Player player) => connectedPlayers.Remove(player.steamId.ToString());

        #region Player Finding

        /// <summary>
        /// Gets all players
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPlayer> All => allPlayers.Values.Cast<IPlayer>();

        /// <summary>
        /// Gets all connected players
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IPlayer> Connected => connectedPlayers.Values.Cast<IPlayer>();

        /// <summary>
        /// Finds a single player given unique ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public IPlayer FindPlayerById(string id)
        {
            PlanetExplorersPlayer player;
            return allPlayers.TryGetValue(id, out player) ? player : null;
        }

        /// <summary>
        /// Finds a single connected player given game object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public IPlayer FindPlayerByObj(object obj) => connectedPlayers.Values.FirstOrDefault(p => p.Object == obj);

        /// <summary>
        /// Finds a single player given a partial name or unique ID (case-insensitive, wildcards accepted, multiple matches returns null)
        /// </summary>
        /// <param name="partialNameOrId"></param>
        /// <returns></returns>
        public IPlayer FindPlayer(string partialNameOrId)
        {
            var players = FindPlayers(partialNameOrId).ToArray();
            return players.Length == 1 ? players[0] : null;
        }

        /// <summary>
        /// Finds any number of players given a partial name or unique ID (case-insensitive, wildcards accepted)
        /// </summary>
        /// <param name="partialNameOrId"></param>
        /// <returns></returns>
        public IEnumerable<IPlayer> FindPlayers(string partialNameOrId)
        {
            foreach (var player in allPlayers.Values)
            {
                if (player.Name != null && player.Name.IndexOf(partialNameOrId, StringComparison.OrdinalIgnoreCase) >= 0 || player.Id == partialNameOrId)
                    yield return player;
            }
        }

        #endregion
    }
}

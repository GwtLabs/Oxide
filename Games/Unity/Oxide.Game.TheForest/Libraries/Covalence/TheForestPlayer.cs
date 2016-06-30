﻿using System;
using System.Linq;

using TheForest.Utils;
using UdpKit;

using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Game.TheForest.Libraries.Covalence
{
    /// <summary>
    /// Represents a player, either connected or not
    /// </summary>
    public class TheForestPlayer : IPlayer, IEquatable<IPlayer>
    {
        private static Permission libPerms;
        private readonly ulong steamId;

        /// <summary>
        /// Gets/sets the name for the player
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the ID for the player (unique within the current game)
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the live player if the player is connected
        /// </summary>
        public ILivePlayer ConnectedPlayer => TheForestCovalenceProvider.Instance.PlayerManager.GetOnlinePlayer(Id);

        internal TheForestPlayer(ulong id, string name)
        {
            // Get perms library
            if (libPerms == null) libPerms = Interface.Oxide.GetLibrary<Permission>();

            // Store user details
            Name = name;
            steamId = id;
            Id = id.ToString();
        }

        #region Permissions

        /// <summary>
        /// Gets if the player has the specified permission
        /// </summary>
        /// <param name="perm"></param>
        /// <returns></returns>
        public bool HasPermission(string perm) => libPerms.UserHasPermission(Id, perm);

        /// <summary>
        /// Grants the specified permission on this user
        /// </summary>
        /// <param name="perm"></param>
        public void GrantPermission(string perm) => libPerms.GrantUserPermission(Id, perm, null);

        /// <summary>
        /// Strips the specified permission from this user
        /// </summary>
        /// <param name="perm"></param>
        public void RevokePermission(string perm) => libPerms.RevokeUserPermission(Id, perm);

        /// <summary>
        /// Gets if the player belongs to the specified usergroup
        /// </summary>
        /// <param name="group"></param>
        /// <returns></returns>
        public bool BelongsToGroup(string group) => libPerms.UserHasGroup(Id, group);

        /// <summary>
        /// Adds the player to the specified usergroup
        /// </summary>
        /// <param name="group"></param>
        public void AddToGroup(string group) => libPerms.AddUserGroup(Id, group);

        /// <summary>
        /// Removes the player from the specified usergroup
        /// </summary>
        /// <param name="group"></param>
        public void RemoveFromGroup(string group) => libPerms.RemoveUserGroup(Id, group);

        #endregion

        #region Administration

        /// <summary>
        /// Bans the player for the specified reason and duration
        /// </summary>
        /// <param name="reason"></param>
        /// <param name="duration"></param>
        public void Ban(string reason, TimeSpan duration = default(TimeSpan))
        {
            // Check already banned
            if (IsBanned) return;

            // Set to banned
            Scene.HudGui.MpPlayerList.Ban(steamId);
            CoopKick.SaveList();

            var entity = Scene.SceneTracker.allPlayerEntities.FirstOrDefault(e => e.source.RemoteEndPoint.SteamId.Id == steamId);
            if (entity != null) CoopKick.KickPlayer(entity, (int)duration.TotalMinutes, reason);
        }

        /// <summary>
        /// Unbans the player
        /// </summary>
        public void Unban()
        {
            // Check not banned
            if (!IsBanned) return;

            // Set to unbanned
            CoopKick.UnBanPlayer(steamId);
        }

        /// <summary>
        /// Gets if the player is banned
        /// </summary>
        public bool IsBanned => CoopKick.IsBanned(new UdpSteamID(steamId));

        /// <summary>
        /// Gets the amount of time remaining on the player's ban
        /// </summary>
        public TimeSpan BanTimeRemaining
        {
            get
            {
                var kickedPlayer = CoopKick.Instance.KickedPlayers.First(k => k.SteamId == steamId);
                return kickedPlayer != null ? TimeSpan.FromTicks(kickedPlayer.BanEndTime) : TimeSpan.Zero;
            }
        }

        #endregion

        #region Chat and Commands

        /// <summary>
        /// Replies to the player with the specified message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void Reply(string message, params object[] args) => ConnectedPlayer.Reply(message, args);

        #endregion

        #region Operator Overloads

        /// <summary>
        /// Returns if player's ID is equal to another player's ID
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(IPlayer other) => Id == other.Id;

        /// <summary>
        /// Gets the hash code of the player's ID
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() => Id.GetHashCode();

        #endregion
    }
}

﻿using System;
using System.IO;
using Craft.Net.Common;
using BufferedStream = Craft.Net.Networking.BufferedStream;

namespace Craft.Net.Networking
{
    public class NetworkManager
    {
        public const int ProtocolVersion = 4;
        public const string FriendlyVersion = "1.7.2";
        public delegate IPacket CreatePacketInstance(PacketDirection direction);

        public NetworkMode NetworkMode { get; private set; }
        public bool Strict { get; set; }

        private BufferedStream BufferedStream { get; set; }
        private MinecraftStream MinecraftStream { get; set; }

        public NetworkManager(Stream stream)
        {
            NetworkMode = NetworkMode.Handshake;
            Strict = true;
            BufferedStream = new BufferedStream(stream);
            MinecraftStream = new MinecraftStream(BufferedStream);
        }

        #region Packet Types
        private static readonly CreatePacketInstance[][] NetworkModes = new CreatePacketInstance[][]
        {
            HandshakePackets,
            StatusPackets,
            LoginPackets,
            PlayPackets
        };

        private static readonly CreatePacketInstance[] HandshakePackets = new CreatePacketInstance[]
        {
            d => new HandshakePacket() // 0x00
        };

        private static readonly CreatePacketInstance[] StatusPackets  = new CreatePacketInstance[]
        {

        };

        private static readonly CreatePacketInstance[] LoginPackets  = new CreatePacketInstance[]
        {

        };

        private static readonly CreatePacketInstance[] PlayPackets  = new CreatePacketInstance[]
        {

        };
        #endregion

        public IPacket ReadPacket(PacketDirection direction)
        {
            int lengthLength, idLength;
            long length = MinecraftStream.ReadVarInt(out lengthLength);
            long id = MinecraftStream.ReadVarInt(out idLength);
            if (NetworkModes[(int)NetworkMode].Length < id || NetworkModes[(int)NetworkMode][id] == null)
            {
                if (Strict)
                    throw new InvalidOperationException("Invalid packet ID: 0x" + id.ToString("X2"));
                else
                {
                    return new UnknownPacket
                    {
                        Id = id,
                        Data = MinecraftStream.ReadUInt8Array((int)(length - lengthLength - idLength)),
                        NetworkMode = NetworkMode
                    };
                }
            }
            var packet = NetworkModes[(int)NetworkMode][id](direction);
            NetworkMode = packet.ReadPacket(MinecraftStream);
            return packet;
        }

        public void WritePacket(IPacket packet)
        {
            packet.WritePacket(MinecraftStream);
            BufferedStream.WriteImmediately = true;
            MinecraftStream.WriteVarInt(packet.Id);
            MinecraftStream.WriteVarInt(BufferedStream.PendingWrites);
            BufferedStream.WriteImmediately = false;
            BufferedStream.Flush();
        }

        /// <summary>
        /// Overrides the implementation for a certain packet.
        /// </summary>
        /// <param name="factory">A method that returns a new instance of the packet for populating later.</param>
        public void OverridePacket(CreatePacketInstance factory)
        {
            if (factory == null)
                throw new ArgumentNullException("factory");
            var packet = factory(PacketDirection.ClientToServer);
            if (packet == null)
                throw new NullReferenceException("Factory must not return null packet.");
            packet = factory(PacketDirection.ServerToClient);
            if (packet == null)
                throw new NullReferenceException("Factory must not return null packet.");
            NetworkModes[(int)NetworkMode][packet.Id] = factory;
        }
    }
}

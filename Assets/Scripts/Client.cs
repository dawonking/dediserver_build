using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;


public class Client
{
    public static int dataBufferSize = 4096;

    public int id;
    public Player player;
    public TCP tcp;
    public UDP udp;

    public Client(int _clientId)
    {
        id = _clientId;
        tcp = new TCP(id);
        udp = new UDP(id);
    }

    public class TCP
    {
        public TcpClient socket;
        private readonly int id;
        private NetworkStream stream;
        private Packet receiveData;
        private byte[] receiveBuffer;

        public TCP(int _id)
        {
            id = _id;
        }

        public void Connect(TcpClient _socket)
        { 
            socket = _socket;
            socket.ReceiveBufferSize = dataBufferSize;
            socket.SendBufferSize = dataBufferSize;

            //�� ��Ʈ��ũ ��Ʈ���� �̿��ؼ� ��Ʈ��ũ���� ����Ÿ �ۼ����ϰ� �ȴ�.
            stream = socket.GetStream();

            receiveData = new Packet();
            receiveBuffer = new byte[dataBufferSize];

            ///�񵿱� �б� ����
            ///����Ʈ �迭 , offset , NetworkStram���� ������ �ִ� ����Ʈ ��, �񵿱� �Ϸ�� ����� �ݹ� , state Object
            ///�бⰡ �Ϸ�Ǹ� �񵿱� �ݹ��� ����
            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            ServerSend.Welcome(id, "Welcome to the server!");
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                //EndRead -> BeginRead�� ���۵� �񵿱� �б� �۾��� �Ϸ�
                int _byteLength = stream.EndRead(result);

                if(_byteLength <= 0) { Server.clients[id].Disconnect(); return; }

                byte[] _data = new byte[_byteLength];
                Array.Copy(receiveBuffer,_data, _byteLength);
                receiveData.Reset(HandleData(_data));
                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch(Exception e)
            {
                Debug.Log($"Error receiving TCP data: {e}");
                Server.clients[id].Disconnect();
            }
        }

        private bool HandleData(byte[] _data)
        {
            int _packetLength = 0;

            //������ array�� ���ۿ� �ֱ�
            receiveData.SetBytes(_data);


            // ���۱��� - ���� �б� ��ġ
            if (receiveData.UnreadLength() >= 4)
            {
                //���� ���� �迭�� �ѱ���
                _packetLength = receiveData.ReadInt();
                if(_packetLength <= 0)
                {
                    // If packet contains no data
                    // ���� ��Ŷ�� �� �о��ٸ� true�� ��ȯ ����
                    return true;
                }                               
            }

            /*
                while���� �̿��� ��Ŷ ����, 
                ��Ŷ ���̰� 0���� ũ�� , ��Ŷũ�Ⱑ ���ų�
                UnreadLength�� ���� ��Ŷ�� ũ�⸦ Ȯ��
                ReadInt�� ��Ŷ�� readPos�� ����
                ThreadManager�� ��Ŷ�� ���� �� Server�� packetHandlers�� packetid�� ���� ServerHandler�� �߰��Ѵ�
                ������� _packetId�� 2�̸� PlayerMovement �̴�.
                PlayerMovement�� ��� �ش� id�� �÷��̾ SetInput�� ����, ������ ������ ������ �����̰� �ȴ�.
                ���� FixedUpdate���� ServerSend.pos , rot�� ���� ����
                SendUDPDataToAll���� ������ ���ο� ����
                �� �÷��̾�� UDP�� ����
                 */

            while (_packetLength > 0 && _packetLength <= receiveData.UnreadLength())
            {                
                byte[] _packetBytes = receiveData.ReadBytes(_packetLength);
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet _packet = new Packet(_packetBytes))
                    {
                        int _packetId = _packet.ReadInt();
                        Server.packetHandlers[_packetId](id, _packet); // Call appropriate method to handle the packet
                    }
                });

                _packetLength = 0;
                if (receiveData.UnreadLength() >= 4)
                {
                    // If client's received data contains another packet
                    _packetLength = receiveData.ReadInt();
                    if (_packetLength <= 0)
                    {
                        //������ ��������
                        return true; // Reset receivedData instance to allow it to be reused
                    }
                }
            }

            if (_packetLength <= 1)
            {
                return true;
            }

            return false;

        }

        public void SendData(Packet _packet)
        {
            try
            {
                if(socket != null)
                {
                    //��Ʈ���� �񵿱� ���� ����
                    //byte�迭 , ��ġ , Netstramũ�� , �񵿱� �ݹ� , state
                    stream.BeginWrite(_packet.ToArray(), 0, _packet.Length(), null, null);
                }
            }
            catch(Exception e)
            {
                Debug.Log($"Error sending data to player {id} via TCP: {e}");
            }
        }
        public void Disconnect()
        {
            socket.Close();
            stream = null;
            receiveData = null;
            receiveBuffer = null;
            socket = null;
        }
    }

    public class UDP
    {
        public IPEndPoint endPoint;
        private int id;

        public UDP(int _id)
        {
            id = _id;
        }

        public void Connect(IPEndPoint _endPoint)
        {
            endPoint = _endPoint;
        }

        public void SendData(Packet _packet)
        {
            Server.SendUDPData(endPoint, _packet);
        }
        public void HandleData(Packet _packetData)
        {
            int _packetLength = _packetData.ReadInt();
            byte[] _packetBytes = _packetData.ReadBytes(_packetLength);

            ThreadManager.ExecuteOnMainThread(() =>
            {
                //����� �ڵ���ȯ
                using (Packet _packet = new Packet(_packetBytes))
                {
                    int _packetId = _packet.ReadInt();
                    Server.packetHandlers[_packetId](id, _packet); // Call appropriate method to handle the packet
                }
            });
        }

        public void Disconnect()
        {
            endPoint = null;
        }

    }

    //����� �÷��̾� ����, ���, ����
    public void SendIntoGame(string _playerName)
    {
        player = NetworkManager.instance.InstantiatePlayer();
        player.Initialize(id, _playerName);

        foreach (Client _client in Server.clients.Values)
        {
            if (_client.player != null)
            {
                //�������̵� �ƴ� ������ �÷��̾�� ����
                if (_client.id != id)
                {
                    ServerSend.SpawnPlayer(id, _client.player);
                }
            }
        }

        // �ڱ��ڽ�����
        //���� ������ �÷��̾�鿡�� ���� �Դٴ°��� ����,
        //�Ʒ��� ���� ������ ������ �÷��̾���� ���� ����
        foreach (Client _client in Server.clients.Values)
        {
            if (_client.player != null)
            {
                ServerSend.SpawnPlayer(_client.id, player);
            }
        }

        //�ʱ� ������ ������ ����
        foreach (ItemSpawner _itemSpawner in ItemSpawner.spawners.Values)
        {
            ServerSend.CreateItemSpawner(id, _itemSpawner.spawnerId, _itemSpawner.transform.position, _itemSpawner.hasItem);
        }
    }

    private void Disconnect()
    {
        Debug.Log($"{tcp.socket.Client.RemoteEndPoint} has disconnected.");

        ThreadManager.ExecuteOnMainThread(() =>
        {
            UnityEngine.Object.Destroy(player.gameObject);
            player = null;
        });

        tcp.Disconnect();
        udp.Disconnect();

        ServerSend.PlayerDisconnected(id);
    }

}

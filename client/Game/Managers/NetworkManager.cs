using System;
using System.Collections.Generic;
using System.IO;
using game.ui;
using GXPEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quobject.SocketIoClientDotNet.Client;
using Debug = game.utils.Debug;

namespace game {
    public class NetworkManager : GameObject {
        private static NetworkManager instance;
        public static NetworkManager Instance => instance ?? (instance = new NetworkManager());

        public NetworkPlayer PlayerData;
        public NetworkRoom ActiveRoom;
        public NetworkMostLikelyTo ActiveMinigame1;
        public NetworkWouldYouRather ActiveMinigame2;
        public NetworkNeverHaveIEver ActiveMinigame3;

        private Socket socket;
        private bool initialized;
        private bool clientLoggedIn;
        private bool joinRoomSuccess;
        private bool startedMinigame1;
        private bool newVoteMinigame1;
        private bool newQuestionMinigame1;
        private bool finishedMinigame1;
        private bool showResultsMinigame1;

        private bool startedMinigame2;
        private bool newVoteMinigame2;
        private bool newQuestionMinigame2;
        private bool showResultsMinigame2;
        private bool finishedMinigame2;
        
        private bool startedMinigame3;
        private bool newVoteMinigame3;
        private bool newQuestionMinigame3;
        private bool showResultsMinigame3;
        private bool finishedMinigame3;

        private bool gotNewMessage;
        private ChatMessage newestMessage;

        public bool RoomsReady;
        public List<NetworkRoom> Rooms;

        private NetworkManager() { }

        public void Initialize() {
            PlayerData = new NetworkPlayer("", Guid.NewGuid().ToString(), "none", "none", -1, false);

            var hosted = File.ReadAllText("data/hosted.txt") == "1";
            var socketURL = "https://saxion-0.ey.r.appspot.com";
            if (!hosted) socketURL = "http://localhost:8080";
            socket = IO.Socket(socketURL);

            SetupSocket();
        }

        public void CreateAccount(string username, int avatarIndex, bool consent) {
            PlayerData.Username = username;
            PlayerData.AvatarIndex = avatarIndex;
            PlayerData.Consent = consent;
            socket.Emit("update_account", PlayerData.JSONString);
        }

        public void CreateAndJoinRoom(string roomName, string roomDesc, string code, bool isNSFW, bool isPublic) {
            ActiveRoom = new NetworkRoom(roomName, roomDesc, Guid.NewGuid().ToString(), code, PlayerData.Location, isPublic, isNSFW);
            ActiveRoom.Players.Add(PlayerData.GUID, PlayerData);
            PlayerData.RoomID = ActiveRoom.GUID;
            socket.Emit("create_room", ActiveRoom.JSONString);
            SceneManager.Instance.LoadScene($"{PlayerData.Location}-Bar");
            SoundManager.Instance.PlaySound("bar_ambiance");
        }

        public void TryJoinRoom(string roomGuid, string roomCode) {
            var roomData = new JObject {["guid"] = roomGuid, ["code"] = roomCode};
            socket.Emit("join_room", roomData.ToString(Formatting.None));
        }

        public void RequestRooms() {
            socket.Emit("request_rooms");
        }

        public void JoinLocation(string location, bool joiningLocation = true) {
            PlayerData.Location = location;
            socket.Emit("set_location", PlayerData.Location);
            SceneManager.Instance.LoadScene(joiningLocation ? $"{location}-Menu" : "Map");
        }

        public void LeaveRoom() {
            socket.Emit("leave_room");
            JoinLocation(PlayerData.Location);
        }

        public void SendMessage(ChatMessage message) {
            socket.Emit("send_message", message.JSONString);
        }

        private void ReceivedMessage(ChatMessage message) {
            ChatElement.ActiveChat.ReceiveMessage(message);
            if (message.SenderGUID != "00000000-0000-0000-0000-000000000000")
                SoundManager.Instance.PlaySound("new_message");
        }

        public void StartMinigame1() {
            var minigame1Data = new NetworkMostLikelyTo(Guid.NewGuid().ToString(), PlayerData.GUID);
            ActiveMinigame1 = minigame1Data;
            var jsonData = minigame1Data.JSONObject;
            jsonData["roomGuid"] = ActiveRoom.GUID;
            socket.Emit("start_minigame_1", jsonData.ToString(Formatting.None));
            socket.Emit("request_minigame_1", jsonData.ToString(Formatting.None));
        }

        public void NextQuestionMinigame1() {
            socket.Emit("request_minigame_1", new JObject {["gameGuid"] = ActiveMinigame1.GUID}.ToString(Formatting.None));
        }

        public void StopPlayingMinigame1() {
            socket.Emit("finish_minigame_1", new JObject {["gameGuid"] = ActiveMinigame1.GUID}.ToString(Formatting.None));
            Minigame1Element.ActiveMinigame.Deinitialize();
        }

        public void VoteMinigame1(string playerGuid) {
            ActiveMinigame1.SetVote(PlayerData.GUID, playerGuid);
            if (ActiveMinigame1.Owner == PlayerData.GUID && ActiveMinigame1.IsQuestionDone) {
                socket.Emit("results_minigame_1", "");
                socket.Emit("voted_minigame_1", new JObject {["guid"] = PlayerData.GUID, ["vote"] = playerGuid, ["redirect"] = false}.ToString(Formatting.None));
                showResultsMinigame1 = true;
            } else {
                socket.Emit("voted_minigame_1", new JObject {["guid"] = PlayerData.GUID, ["vote"] = playerGuid}.ToString(Formatting.None));
            }
        }

        public void StartMinigame2() {
            var minigame2Data = new NetworkWouldYouRather(Guid.NewGuid().ToString(), PlayerData.GUID);
            ActiveMinigame2 = minigame2Data;
            var jsonData = minigame2Data.JSONObject;
            jsonData["roomGuid"] = ActiveRoom.GUID;
            socket.Emit("start_minigame_2", jsonData.ToString(Formatting.None));
            socket.Emit("request_minigame_2", jsonData.ToString(Formatting.None));
        }

        public void NextQuestionMinigame2() {
            socket.Emit("request_minigame_2", new JObject {["gameGuid"] = ActiveMinigame2.GUID}.ToString(Formatting.None));
        }

        public void StopPlayingMinigame2() {
            socket.Emit("finish_minigame_2", new JObject {["gameGuid"] = ActiveMinigame2.GUID}.ToString(Formatting.None));
            Minigame2Element.ActiveMinigame.Deinitialize();
        }

        public void VoteMinigame2(string playerGuid) {
            ActiveMinigame2.SetVote(PlayerData.GUID, playerGuid);
            if (ActiveMinigame2.Owner == PlayerData.GUID && ActiveMinigame2.IsQuestionDone) {
                socket.Emit("results_minigame_2", "");
                socket.Emit("voted_minigame_2", new JObject {["guid"] = PlayerData.GUID, ["vote"] = playerGuid, ["redirect"] = false}.ToString(Formatting.None));
                showResultsMinigame2 = true;
            } else {
                socket.Emit("voted_minigame_2", new JObject {["guid"] = PlayerData.GUID, ["vote"] = playerGuid}.ToString(Formatting.None));
            }
        }
        
        public void StartMinigame3() {
            var minigame3Data = new NetworkNeverHaveIEver(Guid.NewGuid().ToString(), PlayerData.GUID);
            ActiveMinigame3 = minigame3Data;
            var jsonData = minigame3Data.JSONObject;
            jsonData["roomGuid"] = ActiveRoom.GUID;
            socket.Emit("start_minigame_3", jsonData.ToString(Formatting.None));
            socket.Emit("request_minigame_3", jsonData.ToString(Formatting.None));
        }

        public void NextQuestionMinigame3() {
            socket.Emit("request_minigame_3", new JObject {["gameGuid"] = ActiveMinigame3.GUID}.ToString(Formatting.None));
        }

        public void StopPlayingMinigame3() {
            socket.Emit("finish_minigame_3", new JObject {["gameGuid"] = ActiveMinigame3.GUID}.ToString(Formatting.None));
            Minigame3Element.ActiveMinigame.Deinitialize();
        }

        public void VoteMinigame3(string playerGuid) {
            ActiveMinigame3.SetVote(PlayerData.GUID, playerGuid);
            if (ActiveMinigame3.Owner == PlayerData.GUID && ActiveMinigame3.IsQuestionDone) {
                socket.Emit("results_minigame_3", "");
                socket.Emit("voted_minigame_3", new JObject {["guid"] = PlayerData.GUID, ["vote"] = playerGuid, ["redirect"] = false}.ToString(Formatting.None));
                showResultsMinigame3 = true;
            } else {
                socket.Emit("voted_minigame_3", new JObject {["guid"] = PlayerData.GUID, ["vote"] = playerGuid}.ToString(Formatting.None));
            }
        }

        public void PlaySound(string soundId, bool stopAlreadyPlaying) {
            socket.Emit("play_sound", new JObject {["soundId"] = soundId, ["stopAlreadyPlaying"] = stopAlreadyPlaying}.ToString(Formatting.None));
            SoundManager.Instance.PlaySound(soundId, stopAlreadyPlaying);
        }

        public void StopPlayingSound(string soundId) {
            socket.Emit("stop_playing_sound", new JObject {["soundId"] = soundId}.ToString(Formatting.None));
            SoundManager.Instance.StopPlaying(soundId);
        }

        private void Update() {
            if (clientLoggedIn) {
                SceneManager.Instance.LoadScene("FakeLoading");
                clientLoggedIn = false;
            }

            if (gotNewMessage) {
                ReceivedMessage(newestMessage);
                gotNewMessage = false;
            }

            if (joinRoomSuccess) {
                SceneManager.Instance.LoadScene($"{PlayerData.Location}-Bar");
                SoundManager.Instance.PlaySound("bar_ambiance");
                joinRoomSuccess = false;
            }

            if (startedMinigame1) {
                startedMinigame1 = false;
            }

            if (newVoteMinigame1) {
                if (ActiveMinigame1.ActiveQuestionVotes[PlayerData.GUID] != "")
                    Minigame1Element.ActiveMinigame.Initialize(1);
                newVoteMinigame1 = false;
            }

            if (newQuestionMinigame1) {
                Minigame1Element.ActiveMinigame.Initialize(0);
                newQuestionMinigame1 = false;
            }

            if (showResultsMinigame1) {
                Minigame1Element.ActiveMinigame.Initialize(2);
                showResultsMinigame1 = false;
            }

            if (finishedMinigame1) {
                Minigame1Element.ActiveMinigame.Deinitialize();
                finishedMinigame1 = false;
            }
            
            if (startedMinigame2) {
                startedMinigame2 = false;
            }

            if (newVoteMinigame2) {
                if (ActiveMinigame2.ActiveQuestionVotes[PlayerData.GUID] != "")
                    Minigame2Element.ActiveMinigame.Initialize(1);
                newVoteMinigame2 = false;
            }

            if (newQuestionMinigame2) {
                Minigame2Element.ActiveMinigame.Initialize(0);
                newQuestionMinigame2 = false;
            }

            if (showResultsMinigame2) {
                Minigame2Element.ActiveMinigame.Initialize(2);
                showResultsMinigame2 = false;
            }

            if (finishedMinigame2) {
                Minigame2Element.ActiveMinigame.Deinitialize();
                finishedMinigame2 = false;
            }
            
            if (startedMinigame3) {
                startedMinigame3 = false;
            }

            if (newVoteMinigame3) {
                if (ActiveMinigame3.ActiveQuestionVotes[PlayerData.GUID] != "")
                    Minigame3Element.ActiveMinigame.Initialize(1);
                newVoteMinigame3 = false;
            }

            if (newQuestionMinigame3) {
                Minigame3Element.ActiveMinigame.Initialize(0);
                newQuestionMinigame3 = false;
            }

            if (showResultsMinigame3) {
                Minigame3Element.ActiveMinigame.Initialize(2);
                showResultsMinigame3 = false;
            }

            if (finishedMinigame3) {
                Minigame3Element.ActiveMinigame.Deinitialize();
                finishedMinigame3 = false;
            }
        }

        private void SetupSocket() {
            SetupSocket_Basic();
            SetupSocket_Rooms();
            SetupSocket_Sound();
            SetupSocket_Minigames();
        }

        private void SetupSocket_Basic() {
            socket.On("connect", data => {
                Debug.Log("Client connected.");
                if (!initialized)
                    clientLoggedIn = true;
                initialized = true;
            });
            socket.On("disconnect", data => { Debug.Log($"Client disconnected. Reason: {data}"); });
            socket.On("request_account", data => { socket.Emit("request_account_success", PlayerData.JSONString); });
        }

        private void SetupSocket_Rooms() {
            socket.On("request_rooms_success", data => {
                Rooms = new List<NetworkRoom>();
                var objData = (JObject) data;
                foreach (var prop in objData.Properties()) {
                    var roomData = (JObject) objData[prop.Name];
                    var room = new NetworkRoom(roomData.Value<string>("name"), roomData.Value<string>("desc"), roomData.Value<string>("guid"), roomData.Value<string>("code"), roomData.Value<string>("type"), roomData.Value<bool>("pub"), roomData.Value<bool>("nsfw"));
                    Rooms.Add(room);
                }

                RoomsReady = true;
            });
            socket.On("create_room_success", data => { });
            socket.On("join_room_failed", data => { Debug.LogWarning("Socket.IO response not implemented for 'join_room_failed'"); });
            socket.On("join_room_success", data => {
                var roomData = (JObject) data;
                ActiveRoom = new NetworkRoom(roomData.Value<string>("name"), roomData.Value<string>("desc"), roomData.Value<string>("guid"), roomData.Value<string>("code"), roomData.Value<string>("type"), roomData.Value<bool>("pub"), roomData.Value<bool>("nsfw"));
                PlayerData.RoomID = ActiveRoom.GUID;
                ActiveRoom.Players.Add(PlayerData.GUID, PlayerData);
                joinRoomSuccess = true;
                socket.Emit("request_players", ActiveRoom.GUID);
            });
            socket.On("request_players_success", data => {
                var playerData = (JObject) data;
                foreach (var playerId in playerData.Properties()) {
                    var networkPlayerData = new NetworkPlayer(playerData[playerId.Name].Value<string>("username"), playerData[playerId.Name].Value<string>("guid"), playerData[playerId.Name].Value<string>("room"), playerData[playerId.Name].Value<string>("location"), playerData[playerId.Name].Value<int>("avatar"), playerData[playerId.Name].Value<bool>("consent"));
                    ActiveRoom.Players.Add(playerId.Name, networkPlayerData);
                }
            });
            socket.On("client_joined", data => {
                var playerData = (JObject) data;
                newestMessage = new ChatMessage("SERVER", "00000000-0000-0000-0000-000000000000", $"`{playerData.Value<string>("username")}` joined the room!");
                ActiveRoom.Players.Add(playerData.Value<string>("guid"), new NetworkPlayer() {AvatarIndex = playerData.Value<int>("avatar"), Username = playerData.Value<string>("username"), GUID = playerData.Value<string>("guid")});
                SoundManager.Instance.PlaySound("client_joined");
                gotNewMessage = true;
            });

            socket.On("new_message", data => {
                newestMessage = ChatMessage.FromJSON(JObject.Parse(((JObject) data).Value<string>("message")));
                gotNewMessage = true;
            });

            socket.On("client_disconnected", data => {
                var playerData = (JObject) data;
                newestMessage = new ChatMessage("SERVER", "00000000-0000-0000-0000-000000000000", $"`{playerData.Value<string>("username")}` left the room!");
                ActiveRoom.Players.Remove(playerData.Value<string>("guid"));
                SoundManager.Instance.PlaySound("client_left", true);
                gotNewMessage = true;
            });
        }

        private void SetupSocket_Sound() {
            socket.On("play_sound", data => {
                var jsonData = (JObject) data;
                var soundId = jsonData.Value<string>("soundId");
                var stopAlreadyPlaying = jsonData.Value<bool>("stopAlreadyPlaying");
                if (soundId.StartsWith("Song")) {
                    JukeboxElement.ActiveJukebox.CurrentlyPlaying = soundId;
                }
                SoundManager.Instance.PlaySound(soundId, stopAlreadyPlaying);
            });

            socket.On("stop_playing_sound", data => {
                var jsonData = (JObject) data;
                var soundId = jsonData.Value<string>("soundId");
                SoundManager.Instance.StopPlaying(soundId);
            });
        }

        private void SetupSocket_Minigames() {
            SetupSocket_Minigames_1();
            SetupSocket_Minigames_2();
            SetupSocket_Minigames_3();
        }

        private void SetupSocket_Minigames_1() {
            socket.On("started_minigame_1", data => {
                var minigameData = (JObject) data;
                ActiveMinigame1 = new NetworkMostLikelyTo(minigameData.Value<string>("gameGuid"), minigameData.Value<string>("ownerGuid"));
                startedMinigame1 = true;
            });
            socket.On("voted_minigame_1", data => {
                var minigameData = (JObject) data;
                var redirect = minigameData.TryGetValue("redirect", out _);
                ActiveMinigame1.SetVote(minigameData.Value<string>("guid"), minigameData.Value<string>("vote"));
                if (redirect) return;
                if (ActiveMinigame1.Owner == PlayerData.GUID && ActiveMinigame1.IsQuestionDone) {
                    socket.Emit("results_minigame_1", "");
                    showResultsMinigame1 = true;
                } else {
                    newVoteMinigame1 = true;
                }
            });

            socket.On("request_minigame_1", data => {
                var minigameData = (JObject) data;
                var questionIndex = minigameData.Value<int>("question");
                var question = Minigame1Element.GetQuestion(questionIndex);
                ActiveMinigame1.StartNewQuestion(question);
                newQuestionMinigame1 = true;
            });
            socket.On("results_minigame_1", data => { showResultsMinigame1 = true; });
            socket.On("finished_minigame_1", data => { finishedMinigame1 = true; });
        }

        private void SetupSocket_Minigames_2() {
            socket.On("started_minigame_2", data => {
                var minigameData = (JObject) data;
                ActiveMinigame2 = new NetworkWouldYouRather(minigameData.Value<string>("gameGuid"), minigameData.Value<string>("ownerGuid"));
                startedMinigame2 = true;
            });
            socket.On("voted_minigame_2", data => {
                var minigameData = (JObject) data;
                var redirect = minigameData.TryGetValue("redirect", out _);
                ActiveMinigame2.SetVote(minigameData.Value<string>("guid"), minigameData.Value<string>("vote"));
                if (redirect) return;
                if (ActiveMinigame2.Owner == PlayerData.GUID && ActiveMinigame2.IsQuestionDone) {
                    socket.Emit("results_minigame_2", "");
                    showResultsMinigame2 = true;
                } else {
                    newVoteMinigame2 = true;
                }
            });

            socket.On("request_minigame_2", data => {
                var minigameData = (JObject) data;
                var questionIndex = minigameData.Value<int>("question");
                var question = Minigame2Element.GetQuestion(questionIndex);
                ActiveMinigame2.StartNewQuestion(question);
                newQuestionMinigame2 = true;
            });
            socket.On("results_minigame_2", data => { showResultsMinigame2 = true; });
            socket.On("finished_minigame_2", data => { finishedMinigame2 = true; });
        }
        
        private void SetupSocket_Minigames_3() {
            socket.On("started_minigame_3", data => {
                var minigameData = (JObject) data;
                ActiveMinigame3 = new NetworkNeverHaveIEver(minigameData.Value<string>("gameGuid"), minigameData.Value<string>("ownerGuid"));
                startedMinigame3 = true;
            });
            socket.On("voted_minigame_3", data => {
                var minigameData = (JObject) data;
                var redirect = minigameData.TryGetValue("redirect", out _);
                ActiveMinigame3.SetVote(minigameData.Value<string>("guid"), minigameData.Value<string>("vote"));
                if (redirect) return;
                if (ActiveMinigame3.Owner == PlayerData.GUID && ActiveMinigame3.IsQuestionDone) {
                    socket.Emit("results_minigame_3", "");
                    showResultsMinigame3 = true;
                } else {
                    newVoteMinigame3 = true;
                }
            });

            socket.On("request_minigame_3", data => {
                var minigameData = (JObject) data;
                var questionIndex = minigameData.Value<int>("question");
                var question = Minigame3Element.GetQuestion(questionIndex);
                ActiveMinigame3.StartNewQuestion(question);
                newQuestionMinigame3 = true;
            });
            socket.On("results_minigame_3", data => { showResultsMinigame3 = true; });
            socket.On("finished_minigame_3", data => { finishedMinigame3 = true; });
        }

    }
}
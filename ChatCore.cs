using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Td = Telegram.Td;
using TdApi = Telegram.Td.Api;

using System.Threading;
using System.Collections;

namespace TdExample
{
    class ChatCore
    {

        private Td.Client _client = null;
        private readonly Td.ClientResultHandler _defaultHandler = new DefaultHandler();

        private TdApi.AuthorizationState _authorizationState = null;
        private volatile bool _haveAuthorization = false;
        private volatile bool _quiting = false;

        private volatile AutoResetEvent _gotAuthorization = new AutoResetEvent(false);

        private static readonly string _newLine = Environment.NewLine;
        private static readonly string _commandsLine = "Enter command (gc <chatId> - GetChat, me - GetMe, sm <chatId> <message> - SendMessage, lo - LogOut, q - Quit): ";
        public string _currentPrompt = null;

        private volatile static Dictionary<string, string> lookup = new Dictionary<string, string>();



        private Td.Client CreateTdClient()
        {
            Td.Client result = Td.Client.Create(new UpdatesHandler());
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                result.Run();
            }).Start();
            return result;
        }

        private class DefaultHandler : Td.ClientResultHandler
        {
            void Td.ClientResultHandler.OnResult(TdApi.BaseObject @object)
            {
                //Print(@object.ToString());
                Console.WriteLine("Missing 1" + @object.ToString());


            }
        }


        private void Print(string str)
        {
            Console.WriteLine("Print XXXX");
            /*
            if (_currentPrompt != null)
            {
                Console.WriteLine();
            }
            Console.WriteLine(str);
            if (_currentPrompt != null)
            {
                Console.Write(_currentPrompt);
            }
            */
        }


        private class UpdatesHandler : Td.ClientResultHandler
        {
            void Td.ClientResultHandler.OnResult(TdApi.BaseObject @object)
            {
                if (@object is TdApi.UpdateAuthorizationState)
                {
                    //OnAuthorizationStateUpdated((@object as TdApi.UpdateAuthorizationState).AuthorizationState);
                    Console.WriteLine("Missing 2");
                }
                else
                {
                    // Print("Unsupported update: " + @object);
                }
            }
        }

        private void OnAuthorizationStateUpdated(TdApi.AuthorizationState authorizationState)
        {
            if (authorizationState != null)
            {
                _authorizationState = authorizationState;
            }
            if (_authorizationState is TdApi.AuthorizationStateWaitTdlibParameters)
            {
                TdApi.TdlibParameters parameters = new TdApi.TdlibParameters();
                parameters.DatabaseDirectory = "tdlibXXX";
                parameters.UseMessageDatabase = true;
                parameters.UseSecretChats = true;
                parameters.ApiId = 94575;
                parameters.ApiHash = "a3406de8d171bb422bb6ddf3bbd800e2";
                parameters.SystemLanguageCode = "en";
                parameters.DeviceModel = "Desktop";
                parameters.SystemVersion = "Unknown";
                parameters.ApplicationVersion = "1.0";
                parameters.EnableStorageOptimizer = true;

                _client.Send(new TdApi.SetTdlibParameters(parameters), new AuthorizationRequestHandler());
            }
            else if (_authorizationState is TdApi.AuthorizationStateWaitEncryptionKey)
            {
                _client.Send(new TdApi.CheckDatabaseEncryptionKey(), new AuthorizationRequestHandler());
            }
            else if (_authorizationState is TdApi.AuthorizationStateWaitPhoneNumber)
            {
                string phoneNumber = ReadLine("Please enter phone number: ");
                _client.Send(new TdApi.SetAuthenticationPhoneNumber(phoneNumber, false, false), new AuthorizationRequestHandler());
            }
            else if (_authorizationState is TdApi.AuthorizationStateWaitCode)
            {
                string code = ReadLine("Please enter authentication code: ");
                _client.Send(new TdApi.CheckAuthenticationCode(code, "", ""), new AuthorizationRequestHandler());
            }
            else if (_authorizationState is TdApi.AuthorizationStateWaitPassword)
            {
                string password = ReadLine("Please enter password: ");
                _client.Send(new TdApi.CheckAuthenticationPassword(password), new AuthorizationRequestHandler());
            }
            else if (_authorizationState is TdApi.AuthorizationStateReady)
            {
                _haveAuthorization = true;
                _gotAuthorization.Set();
            }
            else if (_authorizationState is TdApi.AuthorizationStateLoggingOut)
            {
                _haveAuthorization = false;
                Print("Logging out");
            }
            else if (_authorizationState is TdApi.AuthorizationStateClosing)
            {
                _haveAuthorization = false;
                Print("Closing");
            }
            else if (_authorizationState is TdApi.AuthorizationStateClosed)
            {
                Print("Closed");
                if (!_quiting)
                {
                    _client = CreateTdClient(); // recreate _client after previous has closed
                }
            }
            else
            {
                Print("Unsupported authorization state:" + _newLine + _authorizationState);
            }
        }

        private class AuthorizationRequestHandler : Td.ClientResultHandler
        {
            void Td.ClientResultHandler.OnResult(TdApi.BaseObject @object)
            {
                if (@object is TdApi.Error)
                {
                    //Print("Receive an error:" + _newLine + @object);
                    //OnAuthorizationStateUpdated(null); // repeat last action
                    Console.WriteLine("Missing 3");
                }
                else
                {
                    // result is already received through UpdateAuthorizationState, nothing to do
                }
            }
        }

        private string ReadLine(string str)
        {
            Console.Write(str);
            _currentPrompt = str;
            var result = Console.ReadLine();
            _currentPrompt = null;
            return result;
        }


        public void start()
        {
            // disable TDLib log
            Td.Log.SetVerbosityLevel(0);
            if (!Td.Log.SetFilePath("tdlib.log"))
            {
                throw new System.IO.IOException("Write access to the current directory is required");
            }

            // create Td.Client
            _client = CreateTdClient();

            // test Client.Execute
            _defaultHandler.OnResult(_client.Execute(new TdApi.GetTextEntities("@telegram /test_command https://telegram.org telegram.me @gif @test")));

            // main loop
            while (!_quiting)
            {
                // await authorization
                _gotAuthorization.Reset();
                _gotAuthorization.WaitOne();

                _client.Send(new TdApi.GetChats(Int64.MaxValue, 0, 100), _defaultHandler); // preload chat list
                while (_haveAuthorization)
                {
                    GetCommand();
                }
            }
        }

        private void GetCommand()
        {
            string command = ReadLine(_commandsLine);
            string[] commands = command.Split(new char[] { ' ' }, 2);
            try
            {
                switch (commands[0])
                {
                    case "gc":
                        _client.Send(new TdApi.GetChat(GetChatId(commands[1])), _defaultHandler);
                        break;
                    case "me":
                        _client.Send(new TdApi.GetMe(), _defaultHandler);
                        break;
                    case "sm":
                        string[] args = commands[1].Split(new char[] { ' ' }, 2);
                        sendMessage(GetChatId(args[0]), args[1]);
                        break;
                    case "lo":
                        _haveAuthorization = false;
                        _client.Send(new TdApi.LogOut(), _defaultHandler);
                        break;
                    case "q":
                        _quiting = true;
                        _haveAuthorization = false;
                        _client.Send(new TdApi.Close(), _defaultHandler);
                        break;
                    default:
                        Print("Unsupported command: " + command);
                        break;
                }
            }
            catch (IndexOutOfRangeException)
            {
                Print("Not enough arguments");
            }
        }


        private static long GetChatId(string arg)
        {
            long chatId = 0;
            try
            {
                chatId = Convert.ToInt64(arg);
            }
            catch (FormatException)
            {
            }
            catch (OverflowException)
            {
            }
            return chatId;
        }


        private void sendMessage(long chatId, string message)
        {
            // initialize reply markup just for testing
            TdApi.InlineKeyboardButton[] row = { new TdApi.InlineKeyboardButton("https://telegram.org?1", new TdApi.InlineKeyboardButtonTypeUrl()), new TdApi.InlineKeyboardButton("https://telegram.org?2", new TdApi.InlineKeyboardButtonTypeUrl()), new TdApi.InlineKeyboardButton("https://telegram.org?3", new TdApi.InlineKeyboardButtonTypeUrl()) };
            TdApi.ReplyMarkup replyMarkup = new TdApi.ReplyMarkupInlineKeyboard(new TdApi.InlineKeyboardButton[][] { row, row, row });

            TdApi.InputMessageContent content = new TdApi.InputMessageText(new TdApi.FormattedText(message, null), false, true);
            _client.Send(new TdApi.SendMessage(chatId, 0, false, false, replyMarkup, content), _defaultHandler);
        }
    }
}

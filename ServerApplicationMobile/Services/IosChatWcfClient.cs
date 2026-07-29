using System.ServiceModel;
using System.ServiceModel.Channels;
using ATekWeb.ATekWebCommonDBData;

namespace ServerApplicationMobile.Services;

// iOS cannot use the runtime-generated proxy created by ChannelFactory<T>.
// This APM-shaped contract and concrete ChannelBase implementation give WCF a
// predeclared proxy while preserving the server's existing contract and actions.
[ServiceContract(
    Name = nameof(IATekChatWebService),
    Namespace = "http://tempuri.org/",
    CallbackContract = typeof(IATekChatWebServiceCallback))]
internal interface IIosChatWebService
{
    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginFirstCall(string strToken, AsyncCallback callback, object state);
    string EndFirstCall(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginRegisterLogin(string strToken, string ClientCredential, AsyncCallback callback, object state);
    bool EndRegisterLogin(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginRegisterLogout(string strToken, string ClientCredential, AsyncCallback callback, object state);
    bool EndRegisterLogout(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginStartChat(string strToken, string ClientCredential, string strStartMessage, AsyncCallback callback, object state);
    bool EndStartChat(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginConnectChat(string strToken, string ClientCredential, string strConnectMessage, AsyncCallback callback, object state);
    bool EndConnectChat(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginSendChatMessageToServiceTech(string strToken, string ClientCredential, string strMessage, AsyncCallback callback, object state);
    bool EndSendChatMessageToServiceTech(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginSendChatMessageToJob(string strToken, string ClientCredential, string strMessage, AsyncCallback callback, object state);
    bool EndSendChatMessageToJob(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginEndChat(string strToken, string ClientCredential, string strMessage, AsyncCallback callback, object state);
    bool EndEndChat(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginDisconnectChat(string strToken, string ClientCredential, AsyncCallback callback, object state);
    bool EndDisconnectChat(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginSendChatMessageToServiceTechFromServiceTech(string strToken, string ClientCredential, string strMessage, AsyncCallback callback, object state);
    bool EndSendChatMessageToServiceTechFromServiceTech(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginChatWatchDog(string strToken, string ClientCredential, AsyncCallback callback, object state);
    bool EndChatWatchDog(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginServiceTechWatchDog(string strToken, string ClientCredential, AsyncCallback callback, object state);
    bool EndServiceTechWatchDog(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginFileTransferRequestFromJob(string strToken, string ClientCredential, string strMessage, AsyncCallback callback, object state);
    bool EndFileTransferRequestFromJob(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginFileTransferRequestFromJobAccepted(string strToken, string ClientCredential, string strMessage, AsyncCallback callback, object state);
    bool EndFileTransferRequestFromJobAccepted(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginFileTransferFromJob(string strToken, string ClientCredential, string strMessage, AsyncCallback callback, object state);
    bool EndFileTransferFromJob(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginIsWritingFromJob(string strToken, string ClientCredential, string strMessage, AsyncCallback callback, object state);
    bool EndIsWritingFromJob(IAsyncResult result);

    [OperationContract(AsyncPattern = true)]
    IAsyncResult BeginIsWritingFromServiceTech(string strToken, string ClientCredential, string strMessage, AsyncCallback callback, object state);
    bool EndIsWritingFromServiceTech(IAsyncResult result);
}

internal sealed class IosChatWcfClient :
    DuplexClientBase<IIosChatWebService>,
    IATekChatWebService
{
    public IosChatWcfClient(
        InstanceContext callback,
        System.ServiceModel.Channels.Binding binding,
        EndpointAddress address)
        : base(callback, binding, address)
    {
    }

    protected override IIosChatWebService CreateChannel() => new IosChatChannel(this);

    public string FirstCall(string token) =>
        Channel.EndFirstCall(Channel.BeginFirstCall(token, null, null));

    public bool RegisterLogin(string token, string credential) =>
        Channel.EndRegisterLogin(Channel.BeginRegisterLogin(token, credential, null, null));

    public bool RegisterLogout(string token, string credential) =>
        Channel.EndRegisterLogout(Channel.BeginRegisterLogout(token, credential, null, null));

    public bool StartChat(string token, string credential, string message) =>
        Channel.EndStartChat(Channel.BeginStartChat(token, credential, message, null, null));

    public bool ConnectChat(string token, string credential, string message) =>
        Channel.EndConnectChat(Channel.BeginConnectChat(token, credential, message, null, null));

    public bool SendChatMessageToServiceTech(string token, string credential, string message) =>
        Channel.EndSendChatMessageToServiceTech(
            Channel.BeginSendChatMessageToServiceTech(token, credential, message, null, null));

    public bool SendChatMessageToJob(string token, string credential, string message) =>
        Channel.EndSendChatMessageToJob(
            Channel.BeginSendChatMessageToJob(token, credential, message, null, null));

    public bool EndChat(string token, string credential, string message) =>
        Channel.EndEndChat(Channel.BeginEndChat(token, credential, message, null, null));

    public bool DisconnectChat(string token, string credential) =>
        Channel.EndDisconnectChat(Channel.BeginDisconnectChat(token, credential, null, null));

    public bool SendChatMessageToServiceTechFromServiceTech(string token, string credential, string message) =>
        Channel.EndSendChatMessageToServiceTechFromServiceTech(
            Channel.BeginSendChatMessageToServiceTechFromServiceTech(token, credential, message, null, null));

    public bool ChatWatchDog(string token, string credential) =>
        Channel.EndChatWatchDog(Channel.BeginChatWatchDog(token, credential, null, null));

    public bool ServiceTechWatchDog(string token, string credential) =>
        Channel.EndServiceTechWatchDog(Channel.BeginServiceTechWatchDog(token, credential, null, null));

    public bool FileTransferRequestFromJob(string token, string credential, string message) =>
        Channel.EndFileTransferRequestFromJob(
            Channel.BeginFileTransferRequestFromJob(token, credential, message, null, null));

    public bool FileTransferRequestFromJobAccepted(string token, string credential, string message) =>
        Channel.EndFileTransferRequestFromJobAccepted(
            Channel.BeginFileTransferRequestFromJobAccepted(token, credential, message, null, null));

    public bool FileTransferFromJob(string token, string credential, string message) =>
        Channel.EndFileTransferFromJob(
            Channel.BeginFileTransferFromJob(token, credential, message, null, null));

    public bool IsWritingFromJob(string token, string credential, string message) =>
        Channel.EndIsWritingFromJob(
            Channel.BeginIsWritingFromJob(token, credential, message, null, null));

    public bool IsWritingFromServiceTech(string token, string credential, string message) =>
        Channel.EndIsWritingFromServiceTech(
            Channel.BeginIsWritingFromServiceTech(token, credential, message, null, null));

    private sealed class IosChatChannel :
        ClientBase<IIosChatWebService>.ChannelBase<IIosChatWebService>,
        IIosChatWebService
    {
        public IosChatChannel(ClientBase<IIosChatWebService> client) : base(client)
        {
        }

        public IAsyncResult BeginFirstCall(string token, AsyncCallback callback, object state) =>
            BeginInvoke("FirstCall", new object[] { token }, callback, state);
        public string EndFirstCall(IAsyncResult result) =>
            (string)EndInvoke("FirstCall", Array.Empty<object>(), result);

        public IAsyncResult BeginRegisterLogin(string token, string credential, AsyncCallback callback, object state) =>
            BeginInvoke("RegisterLogin", new object[] { token, credential }, callback, state);
        public bool EndRegisterLogin(IAsyncResult result) => EndBool("RegisterLogin", result);

        public IAsyncResult BeginRegisterLogout(string token, string credential, AsyncCallback callback, object state) =>
            BeginInvoke("RegisterLogout", new object[] { token, credential }, callback, state);
        public bool EndRegisterLogout(IAsyncResult result) => EndBool("RegisterLogout", result);

        public IAsyncResult BeginStartChat(string token, string credential, string message, AsyncCallback callback, object state) =>
            BeginInvoke("StartChat", new object[] { token, credential, message }, callback, state);
        public bool EndStartChat(IAsyncResult result) => EndBool("StartChat", result);

        public IAsyncResult BeginConnectChat(string token, string credential, string message, AsyncCallback callback, object state) =>
            BeginInvoke("ConnectChat", new object[] { token, credential, message }, callback, state);
        public bool EndConnectChat(IAsyncResult result) => EndBool("ConnectChat", result);

        public IAsyncResult BeginSendChatMessageToServiceTech(string token, string credential, string message, AsyncCallback callback, object state) =>
            BeginInvoke("SendChatMessageToServiceTech", new object[] { token, credential, message }, callback, state);
        public bool EndSendChatMessageToServiceTech(IAsyncResult result) => EndBool("SendChatMessageToServiceTech", result);

        public IAsyncResult BeginSendChatMessageToJob(string token, string credential, string message, AsyncCallback callback, object state) =>
            BeginInvoke("SendChatMessageToJob", new object[] { token, credential, message }, callback, state);
        public bool EndSendChatMessageToJob(IAsyncResult result) => EndBool("SendChatMessageToJob", result);

        public IAsyncResult BeginEndChat(string token, string credential, string message, AsyncCallback callback, object state) =>
            BeginInvoke("EndChat", new object[] { token, credential, message }, callback, state);
        public bool EndEndChat(IAsyncResult result) => EndBool("EndChat", result);

        public IAsyncResult BeginDisconnectChat(string token, string credential, AsyncCallback callback, object state) =>
            BeginInvoke("DisconnectChat", new object[] { token, credential }, callback, state);
        public bool EndDisconnectChat(IAsyncResult result) => EndBool("DisconnectChat", result);

        public IAsyncResult BeginSendChatMessageToServiceTechFromServiceTech(string token, string credential, string message, AsyncCallback callback, object state) =>
            BeginInvoke("SendChatMessageToServiceTechFromServiceTech", new object[] { token, credential, message }, callback, state);
        public bool EndSendChatMessageToServiceTechFromServiceTech(IAsyncResult result) =>
            EndBool("SendChatMessageToServiceTechFromServiceTech", result);

        public IAsyncResult BeginChatWatchDog(string token, string credential, AsyncCallback callback, object state) =>
            BeginInvoke("ChatWatchDog", new object[] { token, credential }, callback, state);
        public bool EndChatWatchDog(IAsyncResult result) => EndBool("ChatWatchDog", result);

        public IAsyncResult BeginServiceTechWatchDog(string token, string credential, AsyncCallback callback, object state) =>
            BeginInvoke("ServiceTechWatchDog", new object[] { token, credential }, callback, state);
        public bool EndServiceTechWatchDog(IAsyncResult result) => EndBool("ServiceTechWatchDog", result);

        public IAsyncResult BeginFileTransferRequestFromJob(string token, string credential, string message, AsyncCallback callback, object state) =>
            BeginInvoke("FileTransferRequestFromJob", new object[] { token, credential, message }, callback, state);
        public bool EndFileTransferRequestFromJob(IAsyncResult result) => EndBool("FileTransferRequestFromJob", result);

        public IAsyncResult BeginFileTransferRequestFromJobAccepted(string token, string credential, string message, AsyncCallback callback, object state) =>
            BeginInvoke("FileTransferRequestFromJobAccepted", new object[] { token, credential, message }, callback, state);
        public bool EndFileTransferRequestFromJobAccepted(IAsyncResult result) => EndBool("FileTransferRequestFromJobAccepted", result);

        public IAsyncResult BeginFileTransferFromJob(string token, string credential, string message, AsyncCallback callback, object state) =>
            BeginInvoke("FileTransferFromJob", new object[] { token, credential, message }, callback, state);
        public bool EndFileTransferFromJob(IAsyncResult result) => EndBool("FileTransferFromJob", result);

        public IAsyncResult BeginIsWritingFromJob(string token, string credential, string message, AsyncCallback callback, object state) =>
            BeginInvoke("IsWritingFromJob", new object[] { token, credential, message }, callback, state);
        public bool EndIsWritingFromJob(IAsyncResult result) => EndBool("IsWritingFromJob", result);

        public IAsyncResult BeginIsWritingFromServiceTech(string token, string credential, string message, AsyncCallback callback, object state) =>
            BeginInvoke("IsWritingFromServiceTech", new object[] { token, credential, message }, callback, state);
        public bool EndIsWritingFromServiceTech(IAsyncResult result) => EndBool("IsWritingFromServiceTech", result);

        private bool EndBool(string operation, IAsyncResult result) =>
            (bool)EndInvoke(operation, Array.Empty<object>(), result);
    }
}

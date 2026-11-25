using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using UnityEngine;

public static class ChamberRelay
{
    static async Task SignIn(){
        if(string.IsNullOrEmpty(Application.cloudProjectId))
            throw new InvalidOperationException("Unity Cloud 프로젝트 연결이 필요합니다. Unity의 Project Settings > Services에서 프로젝트를 연결하고 Relay를 활성화한 후 다시 빌드하세요.");
        var args=Environment.GetCommandLineArgs();
        int profile=Array.IndexOf(args,"-chamber-auth-profile");
        var options=new InitializationOptions();
        if(profile>=0&&profile+1<args.Length)options.SetProfile(args[profile+1]);
        await UnityServices.InitializeAsync(options);
        if(!AuthenticationService.Instance.IsSignedIn)await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
    public static async Task<(RelayServerData Data,string Code)> Host(){
        await SignIn();
        var allocation=await RelayService.Instance.CreateAllocationAsync(ChamberLan.Capacity-1);
        string code=await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        return (AllocationUtils.ToRelayServerData(allocation,"dtls"),code);
    }
    public static async Task<RelayServerData> Join(string code){
        await SignIn();
        var allocation=await RelayService.Instance.JoinAllocationAsync(code.Trim().ToUpperInvariant());
        return AllocationUtils.ToRelayServerData(allocation,"dtls");
    }
}

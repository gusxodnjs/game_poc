// IOSAudioSession.mm
// ─────────────────────────────────────────────────────────────────────────────
// iOS AVAudioSession 카테고리를 Playback 으로 강제 설정.
//
// 배경:
//   Unity iOS 의 기본 AVAudioSession 카테고리는 SoloAmbient — 디바이스
//   "무음 스위치(silent switch)" ON 상태에서 모든 게임 사운드가 무음 처리된다.
//   BGM 재생을 보장하려면 AVAudioSessionCategoryPlayback 으로 변경해야 한다.
//
// 옵션:
//   MixWithOthers — 사용자가 음악 앱을 동시에 재생 중이어도 게임 BGM 이
//   그 위에 깔리도록 한다. 게임 BGM 이 사용자 음악을 끄지 않음.
//   PoC 단계 권장 기본값.
//
// 호출:
//   Unity C# 의 [DllImport("__Internal")] 으로 _ConfigurePlaybackAudioSession()
//   을 호출. SplashScreen.cs 의 BGM 재생 직전에 1회 실행.
// ─────────────────────────────────────────────────────────────────────────────

#import <AVFoundation/AVFoundation.h>

extern "C" void _ConfigurePlaybackAudioSession() {
    NSError *error = nil;
    AVAudioSession *session = [AVAudioSession sharedInstance];

    BOOL ok = [session setCategory:AVAudioSessionCategoryPlayback
                       withOptions:AVAudioSessionCategoryOptionMixWithOthers
                             error:&error];
    if (!ok || error) {
        NSLog(@"[POC] AVAudioSession setCategory failed: %@", error);
        return;
    }

    BOOL active = [session setActive:YES error:&error];
    if (!active || error) {
        NSLog(@"[POC] AVAudioSession setActive failed: %@", error);
        return;
    }

    NSLog(@"[POC] AVAudioSession configured: category=Playback, options=MixWithOthers, active=YES");
}

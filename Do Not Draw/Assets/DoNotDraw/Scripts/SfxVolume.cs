using UnityEngine;

namespace DoNotDraw.Audio
{
    // 스크립트에서 재생/제어하는 모든 효과음이 VolumeManager.sfxVolume를 따라가게 하기 위한 배율.
    // AudioSource.volume을 설정하거나 PlayOneShot을 호출하는 곳에서 이 배율만 곱해서 쓰면 됩니다.
    // 예: audioSource.PlayOneShot(clip, baseVolume * SfxVolume.Scale);
    public static class SfxVolume
    {
        public static float Scale => VolumeManager.Instance != null ? VolumeManager.Instance.sfxVolume : 1f;
    }
}

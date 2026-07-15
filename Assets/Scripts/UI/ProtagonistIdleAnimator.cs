using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>주인공 캐릭터 Image에 16프레임 착석 idle/한숨 애니메이션을 계속 재생합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class ProtagonistIdleAnimator : MonoBehaviour
{
    private const string SheetResourcePath = "Characters/ProtagonistIdleSheet16";
    private const int Columns = 4;
    private const int Rows = 4;

    // 한숨의 준비-내쉬기-회복 구간을 충분히 유지하여 작은 게임 화면에서도 보이게 합니다.
    private static readonly float[] FrameDurations =
    {
        0.80f, 0.80f, 0.80f, 0.80f,
        0.30f, 0.40f,
        0.65f, 0.85f, 0.85f, 0.65f,
        0.40f, 0.50f, 0.60f,
        0.50f, 0.70f, 0.90f
    };

    private Image targetImage;
    private Sprite[] frames;
    private int frameIndex;
    private float elapsed;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
        BuildFrames();
        ShowFrame(0);
    }

    private void OnEnable()
    {
        frameIndex = 0;
        elapsed = 0f;
        ShowFrame(0);
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        // 상점/설정으로 게임 시간이 멈춰도 캐릭터의 호흡은 계속 보이도록 합니다.
        elapsed += Time.unscaledDeltaTime;
        while (elapsed >= FrameDurations[frameIndex])
        {
            elapsed -= FrameDurations[frameIndex];
            frameIndex = (frameIndex + 1) % frames.Length;
            ShowFrame(frameIndex);
        }
    }

    private void BuildFrames()
    {
        Texture2D sheet = Resources.Load<Texture2D>(SheetResourcePath);
        if (sheet == null)
        {
            Debug.LogError($"[ProtagonistIdleAnimator] 스프라이트 시트를 찾지 못했습니다: Resources/{SheetResourcePath}", this);
            return;
        }

        sheet.filterMode = FilterMode.Point;
        sheet.wrapMode = TextureWrapMode.Clamp;

        int cellWidth = sheet.width / Columns;
        int cellHeight = sheet.height / Rows;
        frames = new Sprite[Columns * Rows];

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                int index = row * Columns + column;
                // 모든 프레임을 동일한 셀 크기로 사용해 Image의 중심과 크기가 흔들리지 않게 합니다.
                // 시트는 좌측 상단부터 읽으므로 Unity의 좌측 하단 좌표로 변환합니다.
                float x = column * cellWidth;
                float y = sheet.height - (row + 1) * cellHeight;
                frames[index] = Sprite.Create(
                    sheet,
                    new Rect(x, y, cellWidth, cellHeight),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                frames[index].name = $"ProtagonistIdle_{index + 1:00}";
            }
        }
    }

    private void ShowFrame(int index)
    {
        if (targetImage == null || frames == null || index < 0 || index >= frames.Length) return;
        targetImage.sprite = frames[index];
        targetImage.preserveAspect = true;
    }

    private void OnDestroy()
    {
        if (frames == null) return;
        foreach (Sprite frame in frames)
            if (frame != null) Destroy(frame);
    }
}

/// <summary>GameScene 캐릭터 이미지에 idle 애니메이터를 자동 부착합니다.</summary>
public static class ProtagonistIdleAnimatorBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (SceneManager.GetActiveScene().name != "GameScene") return;
        GameObject character = GameObject.Find("ProtagonistCharacterImage");
        if (character == null || character.GetComponent<ProtagonistIdleAnimator>() != null) return;
        character.AddComponent<ProtagonistIdleAnimator>();
    }
}

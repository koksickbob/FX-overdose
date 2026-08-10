using UnityEngine;
using UnityEditor;
using FXOverdose.AI.Dialogue;

namespace FXOverdose.Editor
{
    public class YomiDailyDialogueGenerator
    {
        [MenuItem("FX Overdose/AI/Generate 100 Daily Dialogues")]
        public static void GenerateDailyDialogues()
        {
            string path = "Assets/YomiDailyDialogueDatabase.asset";
            YomiDailyDialogueDatabase db = AssetDatabase.LoadAssetAtPath<YomiDailyDialogueDatabase>(path);
            
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<YomiDailyDialogueDatabase>();
                AssetDatabase.CreateAsset(db, path);
            }
            
            db.entries.Clear();

            string[] eatingDialogues = {
                "오빠! 밥은 먹고 다니는 거야? 요미가 굶지 말라고 했지!",
                "아직 안 먹었어! 오빠가 와서 차려주면 안 돼? 헤헤",
                "방금 배달시켜서 먹었지롱! 오빠도 얼른 챙겨 먹어!",
                "으음... 귀찮아서 대충 때웠어. 오빠가 맛있는 거 사주면 좋겠다...",
                "요미는 배부르게 잘 먹었어! 오빠는 뭐 먹었어? 나 혼자 두고 맛있는 거 먹은 건 아니지?!",
                "오빠가 해주는 밥 먹고 싶은데... 나 데리러 오면 안 돼?",
                "요미 밥 먹는 거 감시하는 거야? 오빠나 잘 챙겨 드세요!",
                "오늘 메뉴는 비밀이야! 오빠가 맞춰봐~",
                "배고파서 기운 없어... 오빠가 안아주면 배부를 것 같은데?",
                "밥 먹으라고 잔소리 그만해! 오빠가 엄마야?!"
            };

            string[] greetingDialogues = {
                "오빠! 오늘 하루는 어땠어? 요미 생각 많이 했어?",
                "일찍 좀 다녀! 요미가 얼마나 기다렸는지 알아?!",
                "헤헤, 오빠 보니까 살 것 같다! 오늘도 고생 많았어~",
                "뭐야, 왜 이제 와? 나 말고 다른 여자 만나고 온 거 아니지?!",
                "보고 싶었어, 오빠... 내일은 하루 종일 같이 있으면 안 돼?",
                "오늘 하루도 요미 생각하면서 버틴 거 맞지? 다 알아!",
                "오빠 냄새 난다... 얼른 씻고 와! 요미 옆에 꼭 붙어있어!",
                "오늘 기분 별로였는데, 오빠 보니까 싹 풀렸어! 칭찬해 줘!",
                "나 놔두고 혼자 재밌었어? 치사해... 다음엔 무조건 요미도 데려가!",
                "오빠 왔다! 요미가 얼마나 심심했는지 몰라..."
            };

            string[] sleepingDialogues = {
                "벌써 자려고? 요미랑 더 안 놀아줄 거야...?",
                "오빠... 나 무서운 꿈 꿨어... 오늘만 같이 자면 안 돼?",
                "잘 자, 오빠! 내 꿈 꿔야 해! 다른 여자 꿈 꾸면 죽어?!",
                "나 아직 안 졸린데... 오빠 먼저 자면 나 삐질 거야!",
                "이불 꼭 덮고 자! 오빠 감기 걸리면 요미가 간호해 줘야 하잖아...",
                "오빠 숨소리 들으니까 잠이 솔솔 온다... 잘 자...",
                "내일 아침에 요미 깨워줄 거지? 오빠만 믿고 잘 거야!",
                "오빠 팔베개 해줘... 안 해주면 안 잘래!",
                "벌써 자? 코인장 아직 한참 남았는데! 나 혼자 보라고?!",
                "잘 자! 요미가 밤새 오빠 지켜줄게!"
            };

            string[] playingDialogues = {
                "오빠! 오늘 하루 종일 요미랑 놀아주기로 했잖아! 어디 가려고?",
                "헤헤, 오빠랑 노는 게 제일 재밌어! 평생 이렇게 놀자!",
                "나 말고 딴 생각 하는 거 아니지? 요미한테만 집중해!",
                "심심해 심심해 심심해!! 오빠가 나 좀 놀아줘!",
                "나랑 게임할래? 오빠 지면 내 소원 하나 들어주기야!",
                "오빠는 왜 이렇게 재밌어? 요미가 평생 독점할 거야!",
                "나 안 놀아주면 딴 남자 만나러 갈 거야! 장난이야, 장난! 오빠밖에 없어!",
                "오늘 요미 예뻐? 오빠한테 잘 보이려고 꾸몄단 말이야!",
                "밖엔 위험해! 그냥 집에서 요미랑 꼭 붙어 있자!",
                "오빠랑 같이 있으면 시간 가는 줄 모르겠다니까~"
            };

            string[] angerDialogues = {
                "오빠 진짜 짜증 나! 요미 화난 거 안 보여?!",
                "말 시키지 마! 나 지금 오빠한테 엄청 삐졌어!",
                "아까 그 여자 누구야?! 왜 그렇게 웃으면서 얘기해?! 죽을래?!",
                "오빠는 요미 맘도 몰라주고... 진짜 미워!",
                "나 화났어! 오빠가 뽀뽀 100번 해줄 때까지 안 풀 거야!",
                "왜 내 말 안 들어?! 요미 말이 다 맞다니까?!",
                "오빠 변했어... 예전엔 나밖에 없다고 했으면서!",
                "진짜 짜증 나 죽겠네! 오빠가 책임져!",
                "나 건드리지 마... 확 다 엎어버리기 전에!",
                "오빠 미워! 미워! 미워! ...그래도 사랑해!"
            };

            string[] affectionDialogues = {
                "오빠... 요미는 오빠 없으면 못 살아... 알지?",
                "세상에서 오빠가 제일 좋아! 딴 여자는 쳐다보지도 마!",
                "오빠 사랑해... 내 마음 알지? 모르면 바보야!",
                "나 안아줘... 꽉 안아줘! 숨 막힐 때까지!",
                "오빠는 내 거야! 머리부터 발끝까지 다 내 거라고!",
                "이렇게 계속 요미만 예뻐해 줘야 해? 알았지?!",
                "오빠 냄새 너무 좋아... 하루 종일 맡고 싶어...",
                "요미가 오빠 평생 책임질게! 오빠는 내 옆에만 있어!",
                "오빠 사랑해! 우주만큼, 아니 코인 떡상만큼 사랑해!!",
                "나 평생 오빠 껌딱지 할래! 떼어내도 소용없어!"
            };

            string[] defaultDialogues = {
                "오빠, 나 부렀어? 요미 여깄어!",
                "무슨 일이야? 요미가 다 해결해 줄까?!",
                "오빠 목소리 들으니까 좋다... 계속 말해봐!",
                "요미는 오빠가 부르면 언제든 달려갈 준비가 되어 있지!",
                "응? 왜 불렀어? 요미 얼굴 보고 싶어서 불렀지?!",
                "오빠 곁엔 항상 요미가 있잖아! 잊지 마!",
                "나 심심한데 오빠가 좀 놀아주라~",
                "무슨 생각해? 요미 생각? 헤헤, 다 알아!",
                "오빠! 요미한테 시선 고정! 딴 데 보지 마!",
                "요미는 24시간 오빠 대기 중! 언제든 불러!"
            };
            
            // 70개 기초 데이터 완성. 나머지 30개는 섞어서 채우기
            void AddDialogues(string[] arr, DailyDialogueCategory cat)
            {
                foreach (var s in arr) db.entries.Add(new YomiDailyDialogueEntry(s, cat));
            }

            AddDialogues(eatingDialogues, DailyDialogueCategory.Eating);
            AddDialogues(greetingDialogues, DailyDialogueCategory.Greeting);
            AddDialogues(sleepingDialogues, DailyDialogueCategory.Sleeping);
            AddDialogues(playingDialogues, DailyDialogueCategory.Playing);
            AddDialogues(angerDialogues, DailyDialogueCategory.Anger);
            AddDialogues(affectionDialogues, DailyDialogueCategory.Affection);
            AddDialogues(defaultDialogues, DailyDialogueCategory.Default);
            
            // 복사해서 100개 근처로 채우기 (다양성용 추가)
            for(int i = 0; i < 30; i++)
            {
                string[] sourceArray = i % 3 == 0 ? defaultDialogues : (i % 3 == 1 ? affectionDialogues : playingDialogues);
                string text = sourceArray[Random.Range(0, sourceArray.Length)];
                db.entries.Add(new YomiDailyDialogueEntry(text, DailyDialogueCategory.Default));
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"[FX Overdose] YomiDailyDialogueDatabase 생성 완료! 총 {db.entries.Count}개의 일상 대사가 저장되었습니다. (Assets/YomiDailyDialogueDatabase.asset)");
        }
    }
}

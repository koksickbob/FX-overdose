using UnityEngine;
using UnityEditor;
using FXOverdose.Events;
using FXOverdose.Trading;
using System.IO;

public class GenerateEventTemplates
{
    [MenuItem("Tools/Generate Event Templates")]
    public static void GenerateTemplates()
    {
        string dir = "Assets/Resources/Events/Templates";
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // 템플릿 1: 폭락장 롱/숏 강요
        CreateTemplate(dir, "MarketCrash", "폭락장 공포", new EventLogicOptionData[] {
            new EventLogicOptionData {
                OptionType = ChoiceOptionType.Aggressive,
                ForcePosition = TradingController.PositionType.Short,
                ForceLeverage = 50,
                MentalChangeAmount = -20
            },
            new EventLogicOptionData {
                OptionType = ChoiceOptionType.Safe,
                ForcePosition = TradingController.PositionType.None,
                MentalChangeAmount = 10
            },
            new EventLogicOptionData {
                OptionType = ChoiceOptionType.DirectionalLong,
                ForcePosition = TradingController.PositionType.Long,
                ForceLeverage = 100,
                MentalChangeAmount = -40
            }
        });

        // 템플릿 2: 일론머스크 트윗 빔
        CreateTemplate(dir, "MuskTweet", "일론머스크 트윗", new EventLogicOptionData[] {
            new EventLogicOptionData {
                OptionType = ChoiceOptionType.DirectionalLong,
                ForcePosition = TradingController.PositionType.Long,
                ForceLeverage = 20,
                OverrideBeamPercent = 15f,
                OverrideDurationSeconds = 15,
                MentalChangeAmount = 30
            },
            new EventLogicOptionData {
                OptionType = ChoiceOptionType.Safe,
                ForcePosition = TradingController.PositionType.None,
                MentalChangeAmount = -10
            },
            new EventLogicOptionData {
                OptionType = ChoiceOptionType.Aggressive,
                ForcePosition = TradingController.PositionType.Short,
                ForceLeverage = 125,
                MentalChangeAmount = -50
            }
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GenerateEventTemplates] 템플릿 생성 완료!");
    }

    private static void CreateTemplate(string dir, string name, string theme, EventLogicOptionData[] options)
    {
        string path = $"{dir}/Template_{name}.asset";
        EventLogicTemplateSO so = AssetDatabase.LoadAssetAtPath<EventLogicTemplateSO>(path);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<EventLogicTemplateSO>();
            AssetDatabase.CreateAsset(so, path);
        }
        so.TemplateID = name;
        so.ThemeTag = theme;
        so.LogicOptions = options;
        
        EditorUtility.SetDirty(so);
    }
}

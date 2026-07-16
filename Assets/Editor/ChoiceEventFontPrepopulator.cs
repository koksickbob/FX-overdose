#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using TMPro;
using FXOverdose.Events;

namespace FXOverdose.Editor
{
    public static class ChoiceEventFontPrepopulator
    {
        private const string FONT_ASSET_PATH = "Assets/Fonts/PFStardustBold Dynamic SDF.asset";
        private const string SCRIPTS_DIR = "Assets/Scripts";
        private const string EVENTS_DIR = "Assets/Resources/Events";

        [MenuItem("Tools/FX OVERDOSE/Prepopulate Font Asset (Prevent SDF Import Errors)")]
        public static void PrepopulateFontAsset()
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
            if (fontAsset == null)
            {
                Debug.LogWarning($"[ChoiceEventFontPrepopulator] ⚠️ 폰트 에셋을 찾을 수 없습니다: {FONT_ASSET_PATH}");
                return;
            }

            HashSet<char> charSet = new HashSet<char>();

            // 1. 기본 영문, 숫자, 특수문자 (ASCII)
            string ascii = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+-=[]{}|;':\",./<>?~` \\n\\r\\t▲▼■★🔴🟠🟡🟢💡⚡🛡️🩸💬🔄▶️⚠️♥";
            foreach (char c in ascii) charSet.Add(c);

            // 2. 자주 사용되는 한글 자모 및 2,350자 기본 완성형 텍스트 + 기획 시나리오에서 쓰이는 주요 글자들
            AddKoreanBasicCharacters(charSet);

            // 3. 모든 ScriptableObject 에셋 (이벤트, 아이템 데이터, 상점 목록 등 전체) 스캔
            List<string> validDirs = new List<string>();
            if (AssetDatabase.IsValidFolder("Assets/Data")) validDirs.Add("Assets/Data");
            if (AssetDatabase.IsValidFolder("Assets/Resources")) validDirs.Add("Assets/Resources");

            if (validDirs.Count > 0)
            {
                string[] soGuids = AssetDatabase.FindAssets("t:ScriptableObject", validDirs.ToArray());
                foreach (string guid in soGuids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                    if (so != null)
                    {
                        SerializedObject serializedObject = new SerializedObject(so);
                        SerializedProperty prop = serializedObject.GetIterator();
                        while (prop.Next(true))
                        {
                            if (prop.propertyType == SerializedPropertyType.String)
                            {
                                AddStringToSet(charSet, prop.stringValue);
                            }
                        }
                    }
                }
            }

            // 4. Assets/Scripts 및 Editor 내부의 모든 C# 파일 문자열 스캔
            if (Directory.Exists(SCRIPTS_DIR))
            {
                string[] csFiles = Directory.GetFiles(SCRIPTS_DIR, "*.cs", SearchOption.AllDirectories);
                foreach (string file in csFiles)
                {
                    try
                    {
                        string content = File.ReadAllText(file, Encoding.UTF8);
                        AddStringToSet(charSet, content);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ChoiceEventFontPrepopulator] 파일 읽기 오류 ({file}): {ex.Message}");
                    }
                }
            }

            StringBuilder sb = new StringBuilder(charSet.Count);
            foreach (char c in charSet)
            {
                if (!char.IsControl(c) || c == '\n')
                {
                    sb.Append(c);
                }
            }

            string charactersToAdd = sb.ToString();
            Debug.Log($"[ChoiceEventFontPrepopulator] 🔍 총 {charactersToAdd.Length}개의 고유 문자 수집 완료. 폰트 에셋({fontAsset.name})에 아틀라스 베이킹 시도...");

            // TryAddCharacters를 통해 아틀라스에 문자들을 미리 채워넣어 런타임 Dirtying 방지
            bool added = fontAsset.TryAddCharacters(charactersToAdd, out string missingCharacters);

            // 항상 EditorUtility.SetDirty 호출 및 SaveAssets를 통해 확실한 에셋 아틀라스 보존
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (added)
            {
                Debug.Log($"[ChoiceEventFontPrepopulator] 🎉 폰트 아틀라스 프리캐싱 및 저장 완료! 추가/누락 문자 수: {missingCharacters?.Length ?? 0} (런타임 중 TMP_EditorResourceManager 자동 임포트 충돌 원천 차단)");
            }
            else
            {
                Debug.Log($"[ChoiceEventFontPrepopulator] ✅ 수집된 모든 문자({charactersToAdd.Length}개)가 이미 아틀라스에 등록되어 있으며 저장되었습니다.");
            }
        }

        private static void AddStringToSet(HashSet<char> set, string str)
        {
            if (string.IsNullOrEmpty(str)) return;
            foreach (char c in str)
            {
                if (!char.IsControl(c))
                {
                    set.Add(c);
                }
            }
        }

        private static void AddKoreanBasicCharacters(HashSet<char> set)
        {
            // 한글 2,350자 대표 상용 문자열 및 자음 모음
            string basicHangul =
                "가각간갇갈감갑값갓강갖같갚갛개객갠갤갬갭갯갱갸갹갼걀걈걋걍거걱건걷걸검겁것겅겉겋게겐겔겜겝겟겡겨격겪견겯결겸겹겻경곁계곈엘곪곯곳공잊과곽관괄괌괍괏광괘괜괠괩괭괴괸괼굄비굥교굔굘굠굛굣구국군굳굴굵굶굻굼굽굿궁궂궈궉권궐궨궩궤궴궹귀귄귈귐귑귓규균귤그극근귿글긁금급긋긍기긱긴긷길김깁깃깅깊까깍깐깔깜깝깟깡깨깩깬깰깸깹깻깽꺄꺅꺌꺼꺽꺾껀껄껌껍껏껑께껸껼껴껵견결겸꼇경곁꼬꼭꼰꼲꼴꼼꼽꼿꽁꽃꽈꽉꽐꽜꽝꽤꽥꽹꾀꾄열욤꾜꾸꾹꾼꿀꿇꿈꿉꿋꿍꿔능꿩꿰꿴꿸뀀뀁대끼끽낀낄낌낍낏낑나낙낚난낟날낡낢남납낫낭낮낯낱낳내낸낼냄냅냇냉냐냑냔냐냴냠냥너넉넋넌널넒넓넘넙넛넝넣네넥넨넬넴넵넷넁녀녁년녇녈념녑녯녕녘녴녜노녹논놀놂놈놉놋농높놓놔놘놜놨뇌뇐뇔뇜뇹뇻뇨뇩뇬뇰뇸뇹뇻구국군굳굴굵굶굼굽굿궁궂궈궉권궐궤귀귄귈귐귑귓규균귤그극근귿글긁금급긋긍기긱긴긷길김깁깃깅깊다닥닦단닫달닭닮댦담답닷당닿대댁댄댈댐댑댓댕댜더덕던덛덜덞덟덤덥덧덩덫덮데덱덴델뎀뎁뎃뎡뎌뎬뎔뎠뎡도독돈돋돌돎돐돔돕돗동도돼됐되된될됨됩됫됴두둑둔둘둠둡둣둥둬뒀뒈뒝뒤닌될뒴뒵뒷듀듄듈듐듕드득든듣들듦듬듭듯등딫디딕딘딛딜딤딥딧딩딪따딱딴딸땀땁땃땅때땍땐땔땜땹땟땡떠떡떤떨떪떫떰떱떳떵떻떼떽뗀뗄뗌엡뗫뗭또똑똔똘똥똬똴되된될됨됩니다띄띈뜰뜸뜹뜻띠띤띨띰띱띳팅라락란랃랄람랍랏랑랒랖랗래랙랜랠램랩랫랭랴략랸랼럄럎럐러럭런럴럶럼럽럿렁렇레렉렌렐렘렙렛렝려력련렬렴렵렷령례롄롈롐롔로록론롤롬롭롯롱롸롼뢍뢨뢰뢴뢸룀룁룃료룐룔룠루룩룬룰룸룹릇룽뤄뤠뤼륀렬륌륩륫류륙륜률륨륩륫르륵른를름릅릇릉릍릐리릭린릴림립릿링마막만많맏말맑맒맘맙맛망맞맡맣매맥맨맬맴맵맷맹먀먁먈먕머먹먼멀멂멈멉멋멍멎멓메멕멘멜멤еᆸ멧멥며멱면멸몃명몇모목몬몰몲몸몹못몽뫄뫈뫘뫙뫼묀묄묨묫묘묜묠묨무묵문묻물묽묾뭄뭅뭇뭉뭍뭏뭐뭔뭘뭡뭣뭬뮈뮌뮐뮤뮨뮬뮴뮷므믄믈믐믓미믹민믿밀밂밈밉밋밍및밑바박밖반받발밝밞밟밤밥밧방밭배백밴밸뱀뱁뱃뱅뱌뱍뱐뱔뱝뱟버벅번벋벌벎범법벗벙벚베벡벤벧벨벰벱벳벵벼벽변별볌볍볏병볕볘보복본볼봄봅봇봉봐봔봤봬뵈봰봴뵘뵤뵨부북분불붉붊붐붑붓붕붙붚붜붤붰붸뷔뷘뷜뷴뷼뷰븨븀븃븉브븍븐블븜븝븟비빅빈빌빔빕빗빙빚빛빠빡빤빨빪빰빱빳빵빼빽뺀뺄뺌뺍뺏뺑뺘뺙뺨뻐뻑뻔뻘뻠뻣뻥뻬뼈뼉뼘뼙뼛뼝뽀뽁뽄쫄쫌뽑뽓뽕뾔뾰뿌뿍뿐뿔뿜뿝뿟뿡쀼쁘쁜쁠쁨쁩삐삑삔삘삠삡삣삥사삭산삳살삶삼삽삿상샂샄샆샇새색샌샐샘샙샛생샤샥샨샬샴샾샷샹섀서석섞선섣설섦섧섬섭섯성섶세섹센셀셈셉셋셍셔셕션셜셐셒셨셩셰셴셸솅소속손솔솧솜솝솟송솨솩솬솰솸솹솻솽쇄쇈쇌쇔쇗쇘쇠쇰쇱쇳쇼쇽숀숄숌숍숏숌수숙순숟술숨숩숫숭숯숱숲쉐쉑쉔쉘쉠쉥쉬쉭쉰쉴쉼쉽쉿슁슈슉슌슐슘슛슝스슥슨슬슭슴습슷승시식신싣실싫심십싯싱싶싸싹싼쌀쌈쌉쌌쌍쌔쌕쌘쌜쌤쌥쌨쌩써썩썬썰썲썸썹썼썽쎄쎈쎌쏌쏘쏙쏜쏟쏠쏢쏨쏩쏭쏴쏵쏸쐈쐐쐬쐰쐴쐼쐽쑈쑤쑥쑨쑬쑴쑵쑷쑹쓰쓱쓴쓸쓺쓿씀씁씌씨씩씬씰씸씝씟씽아악안앉않알앍앎앓암압앗앙앞애액앤앨앰앱앳앵야약얀얄얇얌얍얏양얕얗얘어억언얹얻얼얽얾엄업없엇엉엊엌엎에엑엔엘엠엡엣엥여역엮연열엶염엽엿영옂옅옆옇예옌옐옘옙옛오옥온올옭옮옰옴옵옷옹옻와왁완왈왐왑왓왕왜왝왠왨왯왱외왼욀욈욉욋요욕욘욜욤욥욧용우욱운울욹욺움웁웃웅워웍원월웜웝웠웡웨웩웬웰웸웹웽위윅윈윌윔윱윗윙유육윤율윰융윶은을을음읍읏응잊이자작잔잖잗잘잚잠잡잣장잦재잭잰잴잼잽잿쟁쟈쟉쟐쟘쟝저적전절젊점접젓정젖제젝젠젤젬젭젯젱져젼졈졉졌졍제조족존졸졺좀좁좃종좆쫓좋좌좍좐길좜좝좟좡죄좬좰좸좹좼좽죠죡죤죌죔죱쥬쥭쥰쥴쥼주죽준줄줆줌줍줏중줘줬줴쥐쥑쥔쥘쥠비쥣증지직진짇질짊짐집짓징짖짙짚짜짝짠짢짤짦짬짭짯짱째짹짼쨀쨈쨉쨘쨩쩌쩍쩐쩔쩜쩝쩟쩡쩨쪄쪘쪼쪽쫀쫄쫌쫍쫏쫑쫓쫘쫙쫠쫴쬐쭈쭉쭌쭐쭘쭙쭝쮸쯔쯤쯧찌찍찐찔찜찝찟찡차착찬찮찰참찹찻창찾채책챈챌챔챕챗챙챠챤챨챰챵처척천철첨첩첫청체첵첸첼쳄쳅쳇쳉쳐쳔쳤쳬쳰촁초촉촌촐촘촙촛총촤촨촹최쵠쵤쵬쵭쵯쵱쵸촉춍추축춘출춤춥춧충춰췄췌췐취긴췰췸췹췻츄츅ㄴ츌츔츙츠측츤츨츰츱츳층치칙친칟칠침칩칫칭카칵칸칼캄캅캇강캐캑캔캘캠캡캣캥캬캭컁커컥컨컫컬컴컵컷컹케켁켄켈켐켭켓켕켜켠켤켬켭켯켱코콕콘콜콤콥콧콩콰콱콴콸쾀압쾃쾅쾌쾡쾨쾬쾔쾸쾹짓효구국군굳굴굵굶굼굽굿궁궂궈궉권궐궤귀귄귈귐귑귓규균귤그극근귿글긁금급긋긍기긱긴긷길김깁깃깅깊타탁탄탈탐탑탓탕태택탠탤탬탭탯탱탸턍터턱턴털텀텁텃텅테텍텐텔템텝텟텡텨텬텼뎡토톡톤톨톰톱토통톺톼퇀퇘퇴퇜퇘퇬퇭투툭툰툴툼투툽툿퉁퉈퉜퉤튀튕튜튠튤튬튱트특튼튿틀틈틉틋팅티틱틴틸팀팁팃팅파팍판팔팜팝팟팡패팩팬팰팸팹팻팽퍄퍅평퍼퍽펀펄펌펍벗펑페펙펜펠펨펩펫펭펴편펼폄폅폈평포폭폰폴폼폽폿퐁퐈퐝푀표푠푤푭푸푹푼풀품풉풋풍풔퓌퓐퓔퓜퓟퓨퓬퓰퓸퓻프픈플픔퓹퓻피픽핀필핌핍핏핑하학한할함합핫항해핵핸핼햄햅햇행햐향허헉헌헐험헙헛헝헤헥헨헬헴헵헷헹혀혁현혈혐협혓형혜호혹혼홀홅홈홉홋홍화확환활화황홰홱홴횃횅회횐횔횸효횬효후훅훈훌훑훔훗홍훠훨훰훵훼훽휀휄휌휭휘휙휜휠휨휩휫휭휴휵휸휼흄흉흐흑흔흘흙흠흡흣흥흩희흰흴흼흽힇히힉힌힐힘힙힛힝";
            foreach (char c in basicHangul)
            {
                set.Add(c);
            }
        }
    }
}
#endif

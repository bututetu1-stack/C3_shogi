/// <summary>
/// 局の流れと駒の能力まわりのバランスの数値。駒ごとの攻撃・防御・体力・動きは PieceData のアセットにある。
/// 自動プレイの実験では、ここを一時的に書き換えて調整前後を比べる（書き換えたら元に戻す）
/// </summary>
public static class BalanceTuning
{
    /// <summary>敵の攻撃の上乗せ = (局 − EnemyAtkDelay) ÷ この値（4・0なら第四局から+1、第八局+2、第十二局+3）</summary>
    public static int EnemyAtkDivisor = 4;
    /// <summary>敵の攻撃の上乗せを何局遅らせるか（体力の上乗せと同じ局で一度に上がらないように）</summary>
    public static int EnemyAtkDelay = 0;
    /// <summary>敵の駒（C3・深海を除く）に局の上乗せが付き始める局。前半は上乗せせず、敵の種類と布陣で難しさを作る（第9版）。
    /// 上乗せは「この局を1とした局の数」を、下の割る数で割った値</summary>
    public static int EnemyScaleFromStage = 9;
    /// <summary>後半の敵の駒の上乗せ: 体力 = (局 − 7) ÷ この値、攻撃 = (局 − 7) ÷ EnemyLateAtkDivisor（第八局から。第十五局で体力+8・攻撃+4）。
    /// 深海・艦娘の上乗せは EnemyAtkDivisor（局 ÷ 4）のまま</summary>
    public static int EnemyLateHpDivisor = 1;
    public static int EnemyLateAtkDivisor = 2;
    /// <summary>敵の体力の上乗せ = 局 ÷ この値（2なら第二局から+1、第十五局で+7）</summary>
    public static int EnemyHpDivisor = 2;
    /// <summary>敵C3の体力の上乗せ = (局−1) ÷ この値</summary>
    public static int EnemyC3HpDivisor = 2;
    /// <summary>敵C3の体力にさらに足す値（自軍のC3とは別に調整するため。2026-10-11 に全クリア37%→30%にするため1）</summary>
    public static int EnemyC3ExtraHP = 1;

    /// <summary>自軍の体力の上乗せ = (局−1) ÷ この値（C3も含む。部の時間を入れたとき4→5）</summary>
    public static int PlayerHpDivisor = 5;
    /// <summary>自軍の防御の上乗せ = (局−1) ÷ この値（C3は除く）</summary>
    public static int PlayerDefDivisor = 5;
    /// <summary>自軍の攻撃の上乗せ = (局−1) ÷ この値（C3は除く。0なら上乗せしない）</summary>
    public static int PlayerAtkDivisor = 0;

    /// <summary>仲間を2枚選べる局（盤が9×9になり、駒の数が一気に増える第五局）</summary>
    public static int[] TwoPickStages = { 5 };

    /// <summary>強化カード「歩兵を消す」を候補に出すか（取ったら次の局から最後まで自軍の歩兵が出ない）</summary>
    public static bool NoPawnsCard = true;

    /// <summary>仲間の候補に素の将棋駒（香・桂・銀・金・角・飛）も出すか（いまは部員と強化だけ）</summary>
    public static bool DraftStandardPieces = false;

    /// <summary>仲間にした素の将棋駒の体力の上乗せ（DraftStandardPieces のときだけ使う）</summary>
    public static int RecruitBonusHP = 1;
    /// <summary>仲間にした飛車・角の攻撃の上乗せ</summary>
    public static int RecruitBonusATK = 1;

    // ---- 駒の能力（駒の攻撃・防御・体力・動きは PieceData のアセット） ----

    /// <summary>ヲツが手番の開始時に作る中華の数（成る前／成った後）</summary>
    public static int WotsuChukaCount = 2;
    public static int WotsuPromotedChukaCount = 3;
    /// <summary>中華を味方のそばに置く（false なら盤のどこか）</summary>
    public static bool ChukaNearAllies = true;
    /// <summary>中華が周りの味方を回復する量</summary>
    public static int ChukaHeal = 3;
    /// <summary>中華を食べた味方の「満腹」（攻撃+1）を1体が受けられる回数（その局のあいだ）</summary>
    public static int ChukaFullMax = 1;
    /// <summary>組み止め: 小錦の隣にいる敵は小錦の隣から離れられない（挑発を当てにできるデコイにする）</summary>
    public static bool KonishikiGrapple = true;
    /// <summary>小錦が攻撃されたときの「同情」（周りの味方の攻撃+1）を1体が受けられる回数（その局のあいだ）</summary>
    public static int KonishikiSympathyMax = 2;
    /// <summary>挑発の引きつけ: 自軍の挑発駒（小錦・潜水）から何マス以内の敵が、その駒を狙って寄ってくるか（0なら引きつけない。敵の思考にかかる）</summary>
    public static int TauntLureRange = 2;
    /// <summary>引きつけの強さ（敵の思考で、挑発駒を攻撃する手と、1マス近づく手に足す点。駒1体の値打ちがおよそ100〜300）</summary>
    public static int TauntLureAttack = 150;
    public static int TauntLureApproach = 50;

    /// <summary>僕のそばに何手番続けていると強化されるか</summary>
    public static int BokuBuffTurns = 2;

    /// <summary>ユウの助走: 敵まで走ったマスのうち、2マス目から1マスごとの攻撃の上乗せ（3マス先なら+2）</summary>
    public static int YuuRunUpPerSquare = 1;

    /// <summary>きぷが冷笑する敵の数（へるは周りの敵すべて）</summary>
    public static int KipuSneerTargets = 2;

    // ---- 練度（部員の成長。RunRoster） ----

    /// <summary>敵を1体倒したときの練度</summary>
    public static int XpPerKill = 1;
    /// <summary>局に勝ったとき盤に残っていた部員の練度</summary>
    public static int XpPerStageSurvived = 1;
    /// <summary>能力が決まったとき（活躍）の練度と、1局でたまる上限</summary>
    public static int XpPerFeat = 1;
    public static int FeatXpMaxPerStage = 1;
    /// <summary>鍛える札の練度、出てくる局、1枠混ざる確率</summary>
    public static int TrainXp = 3;
    public static int TrainFromStage = 3;
    public static float TrainSlotChance = 0.5f;
    /// <summary>★1・★2・★3 に必要な練度（ブロンズは早く、激レアは遅く育つ）</summary>
    public static int[] StarXpBronze = { 3, 7, 12 };
    public static int[] StarXpRare = { 4, 9, 15 };
    public static int[] StarXpSuperRare = { 5, 11, 18 };
    /// <summary>★の効果: ★1で攻撃、★2で体力、★3で防御</summary>
    public static int StarATK = 1;
    public static int StarHP = 2;
    public static int StarDEF = 1;

    // ---- 覚醒（練度★3の部員が、その局で活躍を重ねると覚醒する） ----

    /// <summary>覚醒に必要な、その局の活躍（倒す・能力が決まる）の数</summary>
    public static int AwakenActivities = 3;
    /// <summary>小錦「横綱」の押し出し: 押せたときと、押した先がふさがっていたときのダメージ（防御無視）</summary>
    public static int YokozunaPushDamage = 2;
    public static int YokozunaWallDamage = 4;
    /// <summary>押し出しで相手を動かす（false なら動かさずに張り手で YokozunaPushDamage+1。組み止めは外れない）</summary>
    public static bool YokozunaPushMoves = true;
    /// <summary>覚醒したけい（デバッグ完了）が届く距離</summary>
    public static int KeiAwakenedRange = 2;
    /// <summary>覚醒したなこ: 突撃で倒したときの追加の突撃の回数</summary>
    public static int NakoChainMax = 2;
    /// <summary>覚醒したなこ: ドパを、通り道に敵がいちばん多く並ぶ向きに出す（false なら覚醒前と同じくでたらめな向き）</summary>
    public static bool NakoAwakenedAim = true;
    /// <summary>覚醒したヲツが作る中華の数と、その中華の満腹の上限（1体あたり）</summary>
    public static int WotsuAwakenedChukaCount = 4;
    public static int WotsuAwakenedFullMax = 2;
    /// <summary>覚醒したきぷ: 冷笑が届く距離と数（冷笑された敵は凍って防御−1）</summary>
    public static int KipuAwakenedRange = 2;
    public static int KipuAwakenedTargets = 3;
    /// <summary>覚醒した李白: 裏返すたびの攻撃の上乗せの上限</summary>
    public static int RihakuPoetMax = 3;
    /// <summary>覚醒した李白: 裏返す範囲の広がり（成る前1・成ると2に足す）</summary>
    public static int RihakuAwakenedRadius = 0;
    /// <summary>雷帝「天雷」の落雷の元のダメージ（貫通。局による上乗せは足さない）</summary>
    public static int RaiteiThunderDamage = 2;
    /// <summary>魔王「真・魔王」の雷撃の数</summary>
    public static int MaouAwakenedStrikes = 2;
    /// <summary>AI が能力を意識する（支援役を狙う・僕や中華のそばに寄る・けい／きぷ／小錦の隣を避ける）</summary>
    public static bool AiAbilityAware = true;
    /// <summary>AI が覚醒した駒に上乗せする値打ち（守る・狙う重み）</summary>
    public static int AiAwakenedValue = 60;
    /// <summary>
    /// AI の読みの工夫（陣営ごと。ビットの和）: 1 = 時間が余れば6手先まで、2 = 手の順の工夫（キラー・ヒストリー）、
    /// 4 = 取り合いの延長（静止探索）、8 = 能力の分かる評価、16 = 広く読む（内側の局面で14手ではなく24手）。新旧を対戦させて測るために陣営ごとに分けてある
    /// </summary>
    public static int AiFeaturesPlayer = 10;
    public static int AiFeaturesEnemy = 14;
    /// <summary>
    /// AI の評価の重み（陣営ごと。空なら既定）: [C3 への脅威1つ, C3 の隣の味方1体, 取れる駒の値打ち×/10, 取られそうな駒×/10, 1段の前進, 相手 C3 へ1マス, C3 の体力1]。
    /// 既定は { 450, 150, 8, 3, 3, 4, 300 }。--set AiWeightsEnemy=600,150,8,3,3,4,300 のように試す
    /// </summary>
    public static int[] AiWeightsPlayer = new int[0];
    public static int[] AiWeightsEnemy = { 450, 150, 8, 3, 6, 8, 300 };   // 前進・接近を2倍（後半ほど C3 に迫る。勝率は同じ）
    /// <summary>覚醒した僕が1体を強化できる量の上限</summary>
    public static int BokuAwakenedCap = 1;

    // ---- 2026-10-11 に加わった部員 ----

    /// <summary>光春: 盤にいるあいだ、味方が敵を倒すたびにたまる裏金と上限（周をまたいで残る）</summary>
    public static int MitsuharuFundPerKill = 1;
    /// <summary>光春（会計の権力）: 盤にいるあいだ、自軍の手番の初めにたまる裏金</summary>
    public static int MitsuharuFundPerTurn = 1;
    /// <summary>光春（高飛び）: 裏金1でお土産を送る味方の数</summary>
    public static int MitsuharuSouvenirTargets = 2;
    public static int MitsuharuFundMax = 10;
    /// <summary>光春: 裏金がこれだけたまると、自軍の手番の初めに自分から海外へ旅立つ（敵陣に入っても成る）</summary>
    public static int MitsuharuTripCost = 5;
    /// <summary>光春（高飛び）のお土産: 傷ついた味方への回復（元気なら攻撃+1）</summary>
    public static int MitsuharuSouvenirHeal = 2;
    /// <summary>覚醒した光春（世界一周）: お土産を送る味方の数</summary>
    public static int MitsuharuAwakenedTargets = 4;
    /// <summary>光春: 裏金が尽きたら帰国して、成る前の姿で自陣に戻る</summary>
    public static bool MitsuharuReturnsWhenBroke = true;
    /// <summary>ニコ: 山札の上限（成る前／成った後）</summary>
    public static int NikoCapacity = 3;
    public static int NikoPromotedCapacity = 5;
    /// <summary>覚醒したニコ（神ゲー完成）: 1手番に多く処理する枚数と、札の効果の上乗せ</summary>
    public static int NikoAwakenedExtraTasks = 1;
    public static int NikoAwakenedCardBonus = 1;
    /// <summary>ニコの札（なこ・門人・〆鯖）のダメージの元の値</summary>
    public static int NikoStrikeDamage = 2;
    /// <summary>ニコの札（僕・小錦・ユウ）と翡翠のオーバークロックで1体が上がる量の上限</summary>
    public static int NikoBuffCap = 2;
    /// <summary>〆鯖のレスバ（攻撃されて生き残ったとき言い返す）のダメージの元の値（防御無視）</summary>
    public static int ShimesabaRetort = 1;
    /// <summary>〆鯖（成った後）: 世界が終わるまでの手番と、そのときのダメージの元の値（いちばん体力の多い敵1体・防御無視。2026-10-11 に全体3から単体に）</summary>
    public static int ShimesabaCountdown = 3;
    public static int ShimesabaEndDamage = 8;
    /// <summary>覚醒した〆鯖（完全論破）: レスバの上乗せと、成っているときの世界が終わるまでの手番（成っていなければ世界は終わらない）</summary>
    public static int ShimesabaAwakenedRetortBonus = 1;
    public static int ShimesabaAwakenedCountdown = 2;
    /// <summary>翡翠: 「誘うと来てくれる」を1局に使える回数（負なら何度でも）</summary>
    public static int KawasemiInvites = 1;
    /// <summary>翡翠: パーツを拾う範囲と上限</summary>
    public static int KawasemiScavengeRange = 2;
    public static int KawasemiPartsMax = 10;
    /// <summary>翡翠（最強PC）の当たりの確率 = 元 + パーツ1つごと（上限あり）</summary>
    public static float KawasemiGoodBase = 0.5f;
    public static float KawasemiGoodPerPart = 0.05f;
    public static float KawasemiGoodMax = 0.9f;
    /// <summary>最強PC: ベンチマークのダメージ・水冷の回復</summary>
    public static int KawasemiBenchDamage = 2;
    public static int KawasemiCoolHeal = 2;

    // ---- PlayStation 鯖の部員（RN・寝癖・無糖） ----

    /// <summary>RN: 自分の手番の終わりに得るブロック（次の自分の手番まで、受けるダメージをこの分防ぐ）。成ると・覚醒の上乗せ</summary>
    public static int RnBlock = 2;
    public static int RnPromotedBlock = 3;
    public static int RnAwakenedBlockBonus = 2;
    /// <summary>RN（成った後）: ポケモンスリープ（動かなかった手番の終わりの回復）と SEKIRO の弾き（ブロックで全部防いだら相手に。防御無視）</summary>
    public static int RnSleepHeal = 1;
    public static int RnParryDamage = 1;   // （第9版で体幹に置き換えたので使っていない）
    /// <summary>RN（3本同時プレイ）の SEKIRO: 全部防いだ「弾き」のとき、体幹に足す量（防いだ量に加えて）</summary>
    public static int RnPostureParryBonus = 1;
    /// <summary>忍殺でボス（雷帝・龍神・魔王）が失う体力 = 最大体力 ÷ この値（切り上げ）。ほかの敵はそのまま倒れる</summary>
    public static int RnDeathblowBossRatio = 3;
    /// <summary>寝癖: デイリーが何手番で報酬（攻撃+1）になるか（外跳は短い。覚醒は毎手番）と、報酬の上限（その局のあいだ）</summary>
    public static int NeguDailyEvery = 3;
    public static int NeguPromotedDailyEvery = 2;
    public static int NeguDailyCap = 3;
    public static int NeguAwakenedDailyCap = 5;
    /// <summary>寝癖: えちち連合（物鉄が同じ盤にいるあいだ）の攻撃の上乗せ</summary>
    public static int NeguAllianceATK = 1;
    /// <summary>外跳: 跳ね飛ばした敵が何かにぶつかったときのダメージの元の値（防御無視）</summary>
    public static int NeguBounceWallDamage = 2;
    /// <summary>無糖: 死んで覚える（倒されるたびに周のあいだ攻撃+1）の上限、ボスへの攻撃の上乗せ</summary>
    public static int MutouDeathCap = 3;
    public static int MutouBossBonus = 1;
    /// <summary>無糖（AC）: スタッガーした敵が次に受けるダメージの上乗せ</summary>
    public static int MutouStaggerDamage = 2;

    // ---- 第9版の敵駒 ----

    /// <summary>結界: 能力を止める範囲（周り何マス）</summary>
    public static int KekkaiRange = 2;
    /// <summary>弓兵: 手番の終わりに射る矢のダメージの元の値（防御で減る）</summary>
    public static int YumiheiDamage = 1;
    /// <summary>大盾: 正面から受ける通常の攻撃を減らす量</summary>
    public static int OotateGuard = 2;
    /// <summary>妖狐: 倒されたとき残す狐火の数</summary>
    public static int YoukoFoxfires = 2;

    // ---- 局の合間のできごと（部の時間） ----

    /// <summary>できごとがあるか（false なら勝ったらすぐ仲間選び）</summary>
    public static bool StageEventsOn = true;
    /// <summary>できごとが最初に起きる局（この局の前）と、何局ごとに起きるか</summary>
    public static int EventFromStage = 3;
    public static int EventEvery = 2;
    /// <summary>部室で休む: C3 の体力の上乗せ（最後まで）</summary>
    public static int EventRestC3HP = 1;
    /// <summary>差し入れ: C3 以外の全員の体力の上乗せ（最後まで）</summary>
    public static int EventSnackHP = 1;
    /// <summary>合宿: 部員全員の練度／練習試合: いちばん練度の低い部員の練度</summary>
    public static int EventCampXp = 1;
    public static int EventSparringXp = 3;
    /// <summary>部費が下りた: 引き直しの回数</summary>
    public static int EventFundRerolls = 2;
    /// <summary>強敵に挑む: 仲間を選べる枚数の追加と、その局の敵の体力・攻撃の上乗せ</summary>
    public static int EventChallengePicks = 1;
    public static int EventChallengeEnemyHp = 1;
    public static int EventChallengeEnemyAtk = 1;

    // ---- 提督の艦隊 ----

    /// <summary>空母の開幕航空戦（着任時に深海すべてへ）のダメージの元の値</summary>
    public static int CarrierOpeningDamage = 1;
    /// <summary>空母の空爆で、狙った深海に当てるダメージの元の値（周りの敵2体へは CarrierSplashDamage）</summary>
    public static int CarrierStrikeDamage = 1;
    public static int CarrierSplashDamage = 2;
    /// <summary>空母の空爆で巻き込む、狙った深海の周り2マスの敵の数</summary>
    public static int CarrierSplashTargets = 2;
    /// <summary>制空権確保: 空母（中破していない）が盤にいるあいだ、深海から艦娘へのダメージをこの分減らす（Gemini 第三回）</summary>
    public static int AirSuperiorityGuard = 1;
    /// <summary>対潜: 敵の歩兵と香車の通常の攻撃は、潜水艦にも当たる（爆雷。最低1）。ほかの駒の攻撃は今どおり受けない</summary>
    public static bool EnemyAntiSub = true;
    /// <summary>
    /// 陣形（出撃した艦娘が提督から見てどこにいるかで決まる）: 縦の列 = 単縦陣（艦娘の能力ダメージ+FormationLineAheadDamage）、
    /// 横の列 = 単横陣（艦娘の防御+FormationAbreastDef、潜水艦は対潜を FormationAbreastEvade% で避ける）、それ以外 = 警戒陣（提督の受けるダメージ−FormationVanguardGuard）
    /// </summary>
    public static bool FleetFormations = true;
    public static int FormationLineAheadDamage = 1;
    public static int FormationAbreastDef = 1;
    public static int FormationAbreastEvade = 50;
    public static int FormationVanguardGuard = 1;
    /// <summary>基地航空隊: S勝利した次の局の初めに、敵（C3以外）LandBaseTargets 体に LandBaseDamage（＋局の攻撃の上乗せ、防御無視）</summary>
    public static int LandBaseTargets = 3;
    public static int LandBaseDamage = 1;
    /// <summary>開幕雷撃: 改で出撃した潜水艦（練度★3の提督）が着任のときに撃つ雷撃の元の値（0なら撃たない。Gemini 第三回）</summary>
    public static int SubOpeningTorpedo = 2;
    /// <summary>ケッコンカッコカリ: 生還した物鉄・改がもう一度敵陣に入ったときの最大体力の上乗せ（体力も全快。0なら起きない）</summary>
    public static int RingMaxHp = 2;
    /// <summary>夜間触接: 夜戦に入ったとき軽巡か空母がいれば、夜戦のあいだ艦娘の攻撃にこの値を足す（0なら起きない。Gemini 第三回）</summary>
    public static int NightContactDamage = 0;
    /// <summary>艦娘は提督の周り（入渠できるマス）から出撃する（false なら自陣のどこか）</summary>
    public static bool FleetSortieNearTeitoku = true;

    /// <summary>着任のときに建造する艦娘の数（艦種は重複なし）</summary>
    public static int FleetShips = 2;
    /// <summary>着任から何手（両軍の手の合計）で夜戦に入るか（4なら、着任の手番と次の手番の2巡が昼戦で、3巡目から夜戦）</summary>
    public static int FleetNightAfterMoves = 4;
    /// <summary>提督の隣にいる艦娘が、自軍の手番の開始時に回復する量（入渠）</summary>
    public static int FleetDockHeal = 1;
    /// <summary>艦娘の攻撃（雷撃・砲撃・空爆）のダメージに足す値</summary>
    public static int FleetDamageBonus = 0;
    /// <summary>作戦完了のあとも提督と艦娘が盤に残る（false なら帰投して盤を去る）</summary>
    public static bool FleetStaysAfterVictory = false;
    /// <summary>艦娘は随伴の深海を先に狙い、旗艦は最後に狙う（作戦が長く続く）</summary>
    public static bool FleetEscortsFirst = false;
    /// <summary>随伴艦の壁: 随伴が残っているあいだ、旗艦が艦隊から受けるダメージを半分にする</summary>
    public static bool FlagshipGuard = true;
    /// <summary>生還と再配置: 作戦完了で提督は物鉄に戻って盤に残る（攻撃+1・体力+1、その局ではもう着任しない）</summary>
    public static bool FleetVeteranReturn = true;
    /// <summary>支援艦隊: 作戦完了のあと、その局のあいだ自軍の手番の終わりに支援射撃をする回数（0なら無し）</summary>
    public static int FleetSupportShots = 1;
    /// <summary>支援射撃1回のダメージ（防御を無視）</summary>
    public static int FleetSupportDamage = 1;
    /// <summary>支援艦隊はS勝利（艦娘が1隻も沈まなかった）ときだけ付く</summary>
    public static bool FleetSupportNeedsS = true;
}

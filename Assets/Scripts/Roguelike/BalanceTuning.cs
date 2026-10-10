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
    /// <summary>敵の体力の上乗せ = 局 ÷ この値（2なら第二局から+1、第十五局で+7）</summary>
    public static int EnemyHpDivisor = 2;
    /// <summary>敵C3の体力の上乗せ = (局−1) ÷ この値</summary>
    public static int EnemyC3HpDivisor = 2;
    /// <summary>敵C3の体力にさらに足す値（自軍のC3とは別に調整するため）</summary>
    public static int EnemyC3ExtraHP = 0;

    /// <summary>自軍の体力の上乗せ = (局−1) ÷ この値（C3も含む）</summary>
    public static int PlayerHpDivisor = 4;
    /// <summary>自軍の防御の上乗せ = (局−1) ÷ この値（C3は除く）</summary>
    public static int PlayerDefDivisor = 5;
    /// <summary>自軍の攻撃の上乗せ = (局−1) ÷ この値（C3は除く。0なら上乗せしない）</summary>
    public static int PlayerAtkDivisor = 0;

    /// <summary>仲間を2枚選べる局（盤が9×9になり、駒の数が一気に増える第五局）</summary>
    public static int[] TwoPickStages = { 5 };

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
    /// <summary>覚醒したヲツが作る中華の数と、その中華の満腹の上限（1体あたり）</summary>
    public static int WotsuAwakenedChukaCount = 4;
    public static int WotsuAwakenedFullMax = 2;
    /// <summary>覚醒したきぷ: 冷笑が届く距離と数（冷笑された敵は凍って防御−1）</summary>
    public static int KipuAwakenedRange = 2;
    public static int KipuAwakenedTargets = 3;
    /// <summary>覚醒した李白: 裏返すたびの攻撃の上乗せの上限</summary>
    public static int RihakuPoetMax = 3;
    /// <summary>雷帝「天雷」の落雷の元のダメージ（貫通。局による上乗せは足さない）</summary>
    public static int RaiteiThunderDamage = 2;
    /// <summary>魔王「真・魔王」の雷撃の数</summary>
    public static int MaouAwakenedStrikes = 2;
    /// <summary>AI が能力を意識する（支援役を狙う・僕や中華のそばに寄る・けい／きぷ／小錦の隣を避ける）</summary>
    public static bool AiAbilityAware = true;
    /// <summary>覚醒した僕が1体を強化できる量の上限</summary>
    public static int BokuAwakenedCap = 1;

    // ---- 提督の艦隊 ----

    /// <summary>空母の開幕航空戦（着任時に深海すべてへ）のダメージの元の値</summary>
    public static int CarrierOpeningDamage = 1;
    /// <summary>空母の空爆で、狙った深海に当てるダメージの元の値（周りの敵2体へは CarrierSplashDamage）</summary>
    public static int CarrierStrikeDamage = 1;
    public static int CarrierSplashDamage = 2;

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

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace JustTerminal;

// a named set of colonists, so one command can change all of them
public class PawnGroup : IExposable
{
    public string name;
    public List<Pawn> pawns = new List<Pawn>();

    public void ExposeData()
    {
        Scribe_Values.Look(ref name, "name");
        Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            pawns ??= new List<Pawn>();
            pawns.RemoveAll(p => p == null);
        }
    }
}

// the commands that change the game, only in edit mode
public static class EditCommands
{
    public static readonly List<Command> All = new List<Command>
    {
        new Command { name = "help", args = "[command]", examples = new[] { ("help", "list the edit commands"), ("help work", "how work works, with examples") }, complete = done => done.Length == 0 ? All.Select(c => c.Shown).Concat(Commands.All.Select(c => c.Shown)).Distinct().ToList() : new List<string>(), help = "list the edit commands", run = Help },
        new Command { name = "group", examples = new[] { ("group miners Drill Wolf Kovul", "makes the group miners"), ("group", "lists the groups"), ("group miners", "who is in miners") }, args = "[name] [pawns...]", help = "make a group, group alone lists them", run = Programs.Runner("group"), complete = done => done.Length == 0 ? GroupNames() : PawnWords(false) },
        new Command { name = "ungroup", examples = new[] { ("ungroup miners", "removes the group miners") }, args = "<name>", help = "remove a group", run = Programs.Runner("ungroup"), complete = done => done.Length == 0 ? GroupNames() : new List<string>() },
        new Command { name = "work", examples = new[] { ("work @miners firefight 1", "firefight priority 1 for the miners"), ("work all research 0", "nobody researches"), ("work Tasca", "Tasca's priorities") }, args = "<who> [work] [0-4]", help = "show or set work priorities", run = Programs.Runner("work"), complete = CompleteWork },
        new Command { name = "medicine", examples = new[] { ("medicine all herbal", "herbal medicine or worse for everyone"), ("medicine Tasca best", "best medicine for Tasca"), ("medicine @miners", "the miners' medical care") }, args = "<who> [care]", help = "show or set medical care", run = Programs.Runner("medicine"), complete = done => done.Length == 0 ? PawnWords(true) : done.Length == 1 ? careNames.Select(c => c.word).ToList() : new List<string>() },
        new Command { name = "area", examples = new[] { ("area", "lists the areas"), ("area Tasca home", "Tasca only goes in home"), ("area all unrestricted", "everyone goes anywhere") }, args = "[who] [area]", help = "show or set the allowed area, area alone lists them", run = Programs.Runner("area"), complete = done => done.Length == 0 ? PawnWords(true) : done.Length == 1 ? AreaWords() : new List<string>() },
        new Command { name = "policy", examples = new[] { ("policy", "lists the policies"), ("policy Drill", "Drill's four policies"), ("policy Drill food", "Drill's food policy"), ("policy @miners food lavish", "lavish food for the miners") }, args = "[who] [kind] [name]", help = "show or set apparel, food, drugs or reading policy", run = Programs.Runner("policy"), complete = CompletePolicy },
        new Command { name = "goto", args = "<who> <place|x,z|colonist|pick>", help = "drafts them and sends them to a spot, side by side", run = Places.GotoCmd, complete = done => done.Length == 0 ? PawnWords(true) : done.Length == 1 ? (Recorder.Current?.Places.Select(p => p.name) ?? Enumerable.Empty<string>()).Append("pick").ToList() : new List<string>(), examples = new[] { ("goto @guards gate", "the guards go to the place called gate"), ("goto Tasca 120,84", "Tasca goes to x 120, z 84"), ("goto all pick", "click where everyone goes") } },
        new Command { name = "hunt", args = "<number|kind>", help = "mark wild animals to hunt, hunt off <kind> takes the marks off", run = Programs.Runner("hunt"), examples = new[] { ("hunt 3", "the third in wild"), ("hunt boar", "every boar"), ("hunt off boar", "takes the marks off") } },
        new Command { name = "tame", args = "<number|kind>", help = "mark wild animals to tame, tame off <kind> takes the marks off", run = Programs.Runner("tame"), examples = new[] { ("tame 2", "the second in wild"), ("tame husky", "every husky") } },
        new Command { name = "slaughter", args = "<number|name|kind>", help = "mark your animals to slaughter, slaughter off <kind> takes the marks off", run = Programs.Runner("slaughter"), examples = new[] { ("slaughter chicken", "every chicken"), ("slaughter off 3", "not the third") } },
        new Command { name = "master", args = "[animal] [colonist|none]", help = "who handles an animal", run = Programs.Runner("master"), examples = new[] { ("master", "every animal and its master"), ("master Buster Tasca", "Tasca handles Buster") } },
        new Command { name = "train", args = "<animal> [what] [yes|no]", help = "what an animal is trained in", run = Programs.Runner("train"), examples = new[] { ("train Buster", "what Buster can learn"), ("train Buster guard", "trains Buster to guard"), ("train Buster rescue no", "stops that training") } },
        new Command { name = "forbid", args = "<storage number|name|all>", help = "forbids everything in a stockpile or on shelves", run = Programs.Runner("forbid"), examples = new[] { ("forbid 3", "what's in storage 3"), ("forbid food_shelves", "a link or zone by name") } },
        new Command { name = "allow", args = "<storage number|name|all>", help = "allows everything in a stockpile or on shelves again", run = Programs.Runner("allow"), examples = new[] { ("allow all", "everything in storage") } },
        new Command { name = "draft", args = "<who>", help = "draft colonists", run = Programs.Runner("draft"), complete = done => done.Length == 0 ? PawnWords(true) : new List<string>(), examples = new[] { ("draft @guards", "drafts the guards"), ("draft all", "everyone who can be") } },
        new Command { name = "undraft", args = "<who>", help = "back to work", run = Programs.Runner("undraft"), complete = done => done.Length == 0 ? PawnWords(true) : new List<string>(), examples = new[] { ("undraft all", "everyone back to work") } },
        new Command { name = "schedule", args = "<who> [reset|nightshift|24 letters]", help = "show or set the hours, s sleep, w work, j joy, a anything, m meditate", run = Programs.Runner("schedule"), complete = done => done.Length == 0 ? PawnWords(true) : done.Length == 1 ? new List<string> { "reset", "nightshift" } : new List<string>(), examples = new[] { ("schedule Tasca", "her 24 hours from midnight"), ("schedule @night nightshift", "the night group sleeps 10 to 18"), ("schedule all reset", "back to the game's start") } },
        new Command { name = "role", args = "<pawn> [role|none]", help = "change an ideoligion role, it opens the ritual for it", run = Role, complete = CompleteRole, examples = new[] { ("role Tasca", "roles Tasca can take"), ("role Tasca moral_guide", "opens the ritual to make Tasca the moral guide"), ("role Tasca none", "opens the ritual to take her role away") } },
        new Command { name = "exile", args = "<pawn>", help = "send someone away, the game asks first", run = Exile, complete = done => done.Length == 0 ? PawnWords(false) : new List<string>(), examples = new[] { ("exile Drill", "asks to banish Drill") } },
        new Command { name = "caravan", args = "[town]", help = "form a caravan to a town, caravan alone lists the close ones", run = Caravan, complete = done => done.Length == 0 ? NearTowns().Take(20).Select(t => TownWord(t.town)).ToList() : new List<string>(), examples = new[] { ("caravan", "towns close to the colony and how far"), ("caravan Tifunni", "world map and the caravan window, the route already set") } },
        new Command { name = "exit", examples = new[] { ("exit", "back to the normal commands") }, help = "leave edit mode", run = (sh, _) => sh.SetEditMode(false) }
    };

    static EditCommands()
    {
        foreach (Command c in All)
        {
            c.edit = true;
        }
    }

    public static Command Find(string name) => All.FirstOrDefault(c => c.Shown.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? All.FirstOrDefault(c => c.Matches(name));

    private static void Help(Shell sh, string[] args)
    {
        if (args.Length > 0)
        {
            Command c = Find(args[0]) ?? Commands.Find(args[0]);
            if (c == null)
            {
                sh.Print("no command called " + args[0], Ink.Bad);
                return;
            }
            Commands.HelpFor(sh, c);
            return;
        }
        sh.Print("edit mode, these change the game", Ink.Bright);
        Commands.List(sh, All);
        sh.Print();
        sh.Print("who is a colonist, @group or all, use _ for spaces in names", Ink.Dim);
        sh.Out("examples: ", Ink.Dim);
        sh.Out("group miners Drill Wolf Kovul", Ink.Accent);
        sh.Out(", ", Ink.Dim);
        sh.Print("work @miners firefight 1", Ink.Accent);
        sh.Out("help <command> shows examples for it, like ", Ink.Dim);
        sh.Print("help work", Ink.Accent);
        sh.Print("the normal commands still work here", Ink.Dim);
    }

    // who

    internal static List<Pawn> Colonists() =>
        PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep.OrderBy(p => p.LabelShort).ToList();

    private static List<string> GroupNames() => Recorder.Current?.Groups.Select(g => g.name).ToList() ?? new List<string>();

    private static List<string> PawnWords(bool groups)
    {
        List<string> words = Colonists().Select(p => p.LabelShort.Replace(' ', '_')).ToList();
        if (groups)
        {
            words.Add("all");
            words.AddRange(GroupNames().Select(g => "@" + g));
        }
        return words;
    }

    internal static Pawn FindPawn(string word)
    {
        string name = word.Replace('_', ' ');
        List<Pawn> pawns = Colonists();
        return pawns.FirstOrDefault(p => p.LabelShort.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? pawns.FirstOrDefault(p => p.Name?.ToStringFull.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    // a colonist, @group or all, null and a message when nothing matches
    public static List<Pawn> Who(Shell sh, string word)
    {
        List<Pawn> found = Who(word, out string error);
        if (found == null)
        {
            sh.Print(error, Ink.Bad);
        }
        return found;
    }

    public static List<Pawn> Who(string word, out string error)
    {
        error = null;
        if (word.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return Colonists();
        }
        if (word.StartsWith("@"))
        {
            PawnGroup g = Recorder.Current?.FindGroup(word.Substring(1));
            if (g == null)
            {
                error = "no group called " + word.Substring(1);
                return null;
            }
            List<Pawn> alive = g.pawns.Where(p => p != null && !p.Dead && !p.Destroyed && p.IsColonist).ToList();
            if (alive.Count == 0)
            {
                error = "group " + g.name + " has no colonists left";
                return null;
            }
            return alive;
        }
        Pawn pawn = FindPawn(word);
        if (pawn == null)
        {
            error = "no colonist called " + word.Replace('_', ' ');
            return null;
        }
        return new List<Pawn> { pawn };
    }

    private static string Names(IEnumerable<Pawn> pawns) => string.Join(", ", pawns.Select(p => p.LabelShort));

    // ideoligion roles, changed through the game's role change ritual

    private static string RoleWord(Precept_Role r, Pawn p) => r.LabelForPawn(p).Replace(' ', '_').ToLowerInvariant();

    private static List<string> CompleteRole(string[] done)
    {
        if (done.Length == 0)
        {
            return PawnWords(false);
        }
        Pawn p = done.Length == 1 ? FindPawn(done[0]) : null;
        if (p?.Ideo == null)
        {
            return new List<string>();
        }
        List<string> words = RitualUtility.AllRolesForPawn(p).Select(r => RoleWord(r, p)).ToList();
        words.Add("none");
        return words;
    }

    private static void Role(Shell sh, string[] args)
    {
        if (!ModsConfig.IdeologyActive)
        {
            sh.Print("roles need Ideology", Ink.Bad);
            return;
        }
        if (args.Length == 0)
        {
            sh.Print("usage: role <pawn> [role|none]", Ink.Dim);
            return;
        }
        Pawn p = FindPawn(args[0]);
        if (p == null)
        {
            sh.Print("no colonist called " + args[0].Replace('_', ' '), Ink.Bad);
            return;
        }
        if (p.Ideo == null)
        {
            sh.Print(p.LabelShort + " has no ideoligion", Ink.Bad);
            return;
        }
        Precept_Role current = p.Ideo.GetRole(p);
        Ideo primary = Faction.OfPlayer.ideos.PrimaryIdeo;
        List<Precept_Role> roles = RitualUtility.AllRolesForPawn(p).OrderBy(r => r.def.displayOrderInImpact).ToList();
        string Problem(Precept_Role r)
        {
            if (r == current)
            {
                return "has it now";
            }
            if (!r.Active)
            {
                return "not active, needs more believers";
            }
            if (!r.RequirementsMet(p))
            {
                return r.GetFirstUnmetRequirement(p)?.GetLabel(r) ?? "requirements not met";
            }
            if (r.def.leaderRole && p.Ideo != primary)
            {
                return "only for the main ideoligion";
            }
            return null;
        }
        if (args.Length == 1)
        {
            sh.Out(p.LabelShort + "  ", Ink.Bright);
            sh.Print(current != null ? "is " + current.LabelForPawn(p) : "has no role", Ink.Normal);
            foreach (Precept_Role r in roles)
            {
                string problem = Problem(r);
                Pawn holder = r.ChosenPawnSingle();
                sh.Out("  " + Commands.Pad(RoleWord(r, p), 24), problem == null ? Ink.Normal : Ink.Dim);
                if (holder != null && holder != p)
                {
                    sh.Out("now " + holder.LabelShort + "  ", Ink.Dim);
                }
                sh.Print(problem ?? "", r == current ? Ink.Good : Ink.Dim);
            }
            return;
        }
        Precept_Ritual ritual = (Precept_Ritual)p.Ideo.GetPrecept(PreceptDefOf.RoleChange);
        TargetInfo target = ritual?.targetFilter.BestTarget(p, TargetInfo.Invalid) ?? TargetInfo.Invalid;
        if (!target.IsValid)
        {
            sh.Print("there's no place to hold the role change ritual", Ink.Bad);
            return;
        }
        Precept_Role role = null;
        if (!args[1].Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            string wanted = string.Join(" ", args.Skip(1)).Replace('_', ' ');
            role = roles.FirstOrDefault(r => r.LabelForPawn(p).Equals(wanted, StringComparison.OrdinalIgnoreCase) || r.def.label.Equals(wanted, StringComparison.OrdinalIgnoreCase));
            if (role == null)
            {
                sh.Print("no role called " + wanted + ", role " + args[0] + " lists them", Ink.Bad);
                return;
            }
            string problem = Problem(role);
            if (problem != null)
            {
                sh.Print(p.LabelShort + " can't take it: " + problem, Ink.Bad);
                return;
            }
        }
        else if (current == null)
        {
            sh.Print(p.LabelShort + " has no role", Ink.Dim);
            return;
        }
        Dialog_BeginRitual dialog = (Dialog_BeginRitual)ritual.GetRitualBeginWindow(target, null, null, p, new Dictionary<string, Pawn> { { "role_changer", p } });
        dialog.SetRoleToChangeTo(role);
        Verse.Find.WindowStack.Add(dialog);
        sh.Print("the ritual window is open, start it there", Ink.Good);
    }

    // exile, the game's own banish with its warning

    private static void Exile(Shell sh, string[] args)
    {
        if (args.Length == 0)
        {
            sh.Print("usage: exile <pawn>", Ink.Dim);
            return;
        }
        Pawn p = FindPawn(args[0]);
        if (p == null)
        {
            sh.Print("no colonist called " + args[0].Replace('_', ' '), Ink.Bad);
            return;
        }
        PawnBanishUtility.ShowBanishPawnConfirmationDialog(p);
        sh.Print("the game asks to confirm", Ink.Warn);
    }

    // caravans

    private static string TownWord(Settlement t) => t.Label.Replace(' ', '_');

    internal static IEnumerable<(Settlement town, float tiles)> NearTowns()
    {
        Map map = Commands.HomeMap;
        if (map == null)
        {
            return Enumerable.Empty<(Settlement, float)>();
        }
        PlanetTile home = map.Tile;
        return Verse.Find.WorldObjects.Settlements
            .Where(t => t.Faction != Faction.OfPlayer && t.Tile.Layer == home.Layer)
            .Select(t => (t, Verse.Find.WorldGrid.ApproxDistanceInTiles(home, t.Tile)))
            .OrderBy(x => x.Item2);
    }

    private static void Caravan(Shell sh, string[] args)
    {
        Map map = Commands.HomeMap;
        if (map == null)
        {
            sh.Print("no colony map", Ink.Bad);
            return;
        }
        if (args.Length == 0)
        {
            foreach ((Settlement town, float tiles) in NearTowns().Take(15))
            {
                FactionRelationKind kind = town.Faction.PlayerRelationKind;
                sh.Out(Commands.Pad(TownWord(town), 24), Ink.Bright);
                sh.Out(Commands.Pad(town.Faction.Name, 28), Ink.Dim);
                sh.Out(Commands.Pad(kind.GetLabel(), 10), kind == FactionRelationKind.Hostile ? Ink.Bad : kind == FactionRelationKind.Ally ? Ink.Good : Ink.Warn);
                sh.Print($"{tiles:0} tiles".PadLeft(10));
            }
            return;
        }
        string wanted = string.Join(" ", args).Replace('_', ' ');
        Settlement found = NearTowns().Select(x => x.town).FirstOrDefault(t => t.Label.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            ?? NearTowns().Select(x => x.town).FirstOrDefault(t => t.Label.StartsWith(wanted, StringComparison.OrdinalIgnoreCase));
        if (found == null)
        {
            sh.Print("no town called " + wanted + ", caravan alone lists them", Ink.Bad);
            return;
        }
        // the world map behind, then the game's caravan window with the route already chosen
        CameraJumper.TryJump(found.Tile);
        Verse.Find.WorldSelector.ClearSelection();
        Verse.Find.WorldSelector.Select(found);
        // the caravan window starts in the game's route planner, add the town as the waypoint and accept it like the Accept button does
        Dialog_FormCaravan dialog = new Dialog_FormCaravan(map);
        Verse.Find.WindowStack.Add(dialog);
        Verse.Find.WorldRoutePlanner.TryAddWaypoint(found.Tile, false);
        Verse.Find.WindowStack.Add(dialog);
        dialog.Notify_ChoseRoute(found.Tile);
        Verse.Find.WorldRoutePlanner.Stop();
        sh.Print("caravan to " + found.Label + ", pick who and what goes in the caravan window", Ink.Good);
    }

    // groups



    // work

    internal static IEnumerable<WorkTypeDef> WorkTypes => DefDatabase<WorkTypeDef>.AllDefs.Where(w => w.visible).OrderByDescending(w => w.naturalPriority);

    internal static string WorkWord(WorkTypeDef w) => w.labelShort.Replace(' ', '_').ToLowerInvariant();

    internal static WorkTypeDef FindWork(string word)
    {
        string name = word.Replace('_', ' ');
        return WorkTypes.FirstOrDefault(w => w.labelShort.Equals(name, StringComparison.OrdinalIgnoreCase) || w.defName.Equals(word, StringComparison.OrdinalIgnoreCase) || w.gerundLabel.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? WorkTypes.FirstOrDefault(w => w.labelShort.StartsWith(name, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> CompleteWork(string[] done)
    {
        if (done.Length == 0)
        {
            return PawnWords(true);
        }
        if (done.Length == 1)
        {
            return WorkTypes.Select(WorkWord).ToList();
        }
        return done.Length == 2 ? new List<string> { "0", "1", "2", "3", "4" } : new List<string>();
    }



    // medicine

    internal static readonly (string word, MedicalCareCategory care)[] careNames =
    {
        ("none", MedicalCareCategory.NoCare),
        ("nomeds", MedicalCareCategory.NoMeds),
        ("herbal", MedicalCareCategory.HerbalOrWorse),
        ("industrial", MedicalCareCategory.NormalOrWorse),
        ("best", MedicalCareCategory.Best)
    };


    // area

    internal static IEnumerable<Area> Areas(Map map) => map?.areaManager.AllAreas.Where(a => a.AssignableAsAllowed()) ?? Enumerable.Empty<Area>();

    internal static string AreaWord(Area a) => a.Label.Replace(' ', '_').ToLowerInvariant();

    private static List<string> AreaWords()
    {
        List<string> words = Areas(Commands.HomeMap).Select(AreaWord).ToList();
        words.Add("unrestricted");
        return words;
    }


    // policies

    internal static readonly string[] kinds = { "apparel", "food", "drugs", "reading" };

    internal static IEnumerable<Policy> Policies(string kind)
    {
        Game game = Current.Game;
        return kind switch
        {
            "apparel" => game.outfitDatabase.AllOutfits.Cast<Policy>(),
            "food" => game.foodRestrictionDatabase.AllFoodRestrictions.Cast<Policy>(),
            "drugs" => game.drugPolicyDatabase.AllPolicies.Cast<Policy>(),
            "reading" => game.readingPolicyDatabase.AllReadingPolicies.Cast<Policy>(),
            _ => Enumerable.Empty<Policy>()
        };
    }

    internal static string PolicyWord(Policy p) => p.label.Replace(' ', '_').ToLowerInvariant();

    internal static Policy CurrentPolicy(Pawn p, string kind) => kind switch
    {
        "apparel" => p.outfits?.CurrentApparelPolicy,
        "food" => p.foodRestriction?.CurrentFoodPolicy,
        "drugs" => p.drugs?.CurrentPolicy,
        "reading" => p.reading?.CurrentPolicy,
        _ => null
    };

    internal static bool SetPolicy(Pawn p, string kind, Policy policy)
    {
        switch (kind)
        {
            case "apparel" when p.outfits != null:
                p.outfits.CurrentApparelPolicy = (ApparelPolicy)policy;
                return true;
            case "food" when p.foodRestriction != null:
                p.foodRestriction.CurrentFoodPolicy = (FoodPolicy)policy;
                return true;
            case "drugs" when p.drugs != null:
                p.drugs.CurrentPolicy = (DrugPolicy)policy;
                return true;
            case "reading" when p.reading != null:
                p.reading.CurrentPolicy = (ReadingPolicy)policy;
                return true;
        }
        return false;
    }

    private static List<string> CompletePolicy(string[] done)
    {
        if (done.Length == 0)
        {
            return PawnWords(true);
        }
        if (done.Length == 1)
        {
            return kinds.ToList();
        }
        return done.Length == 2 ? Policies(done[1].ToLowerInvariant()).Select(PolicyWord).ToList() : new List<string>();
    }

}

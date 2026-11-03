# pawn: one colonist in detail, like pawn Tasca
# names with spaces use _, like pawn Red_Tasca

if args() = 0
  print dim usage: pawn <name>
  stop
end
if not exists("/pawns/" + arg(1))
  print bad no colonist called {arg(1)}
  stop
end
p = path("/pawns/" + arg(1))

print bright {p/fullname}
print dim {p/gender}, {p/age} years, {p/title}
print
out dim {pad("doing", 9)}
print {p/doing}
out dim {pad("where", 9)}
print {p/where}
out dim {pad("pain", 9)}
print {round(p/pain)}%
print

# the needs, the ones a pawn doesn't have read none and are left out
out dim {pad("health", 9)}
bar p/health 20
print  {round(p/health)}%
if p/mood != none
  out dim {pad("mood", 9)}
  bar p/mood 20
  print  {round(p/mood)}%
end
if p/food != none
  out dim {pad("food", 9)}
  bar p/food 20
  print  {round(p/food)}%
end
if p/rest != none
  out dim {pad("rest", 9)}
  bar p/rest 20
  print  {round(p/rest)}%
end
if p/joy != none
  out dim {pad("joy", 9)}
  bar p/joy 20
  print  {round(p/joy)}%
end

if exists(p/skills)
  print
  print dim SKILLS
  for s in p/skills
    out {pad(s/name, 14)}
    if s/disabled = yes
      print dim -
    else
      out bright {padl(s/level, 2) + " "}
      bar (s/level * 5) 20 normal
      if s/passion = "major"
        print warn  ♥♥
      else if s/passion = "minor"
        print warn  ♥
      else
        print
      end
    end
  end
end

if p/traits != none
  print
  out dim {pad("traits", 9)}
  print {p/traits}
end

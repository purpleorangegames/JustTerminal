# the best colonist at each skill

# the skills come from the first colonist, they all have the same ones
first = none
for p in /pawns
  if first = none
    first = p
  end
end

for s in first/skills
  skill = name(s)
  best = "nobody"
  level = -1
  for p in /pawns
    if exists(p/skills/{skill}) and p/skills/{skill}/disabled = no and p/skills/{skill}/level > level
      best = p/name
      level = p/skills/{skill}/level
    end
  end
  out {pad(s/name, 14)}
  if level < 0
    print dim nobody
  else
    out bright {pad(best, 14)}
    bar (level * 5) 20 normal
    print  {level}
  end
end

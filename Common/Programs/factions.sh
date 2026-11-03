# factions: every faction, how they see us and their leader

out dim {pad("FACTION", 30)}
out dim {pad("RELATION", 10)}
print dim GOODWILL

for f in /factions
  out bright {pad(f/name, 30)}
  ink = "warn"
  if f/relation = "ally"
    ink = "good"
  else if f/relation = "hostile"
    ink = "bad"
  end
  out [ink] {pad(f/relation, 10)}

  if f/goodwill = none
    print dim -
  else
    # -100 to 100, bad on the left of the middle line and good on the right
    g = f/goodwill
    left = rbar(-min(g, 0), 10)
    right = bar(max(g, 0), 10)
    out dim {repeat("·", 10 - len(left))}
    out bad {left}
    out dim │
    out good {right}
    out dim {repeat("·", 10 - len(right))}
    print [ink] {" " + padl(g, 4)}
  end

  out dim {"  " + pad(f/kind, 28)}
  if f/leader != none
    print dim leader {f/leader}
  else
    print
  end
end

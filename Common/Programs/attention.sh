# attention: what needs you now, the game's alerts, wounds no one treated and idle colonists

n = 0
for a in /alerts/list
  if a/priority = "critical"
    print bad {a/name}
  else if a/priority = "high"
    print warn {a/name}
  else
    print {a/name}
  end
  n = n + 1
end

for p in /health/list
  if count(p/open) > 0
    print bad {p/name} has wounds no one treated
    n = n + 1
  end
end

if /idle/count > 0
  print warn nothing to do: {/idle/names}
  n = n + 1
end

if n = 0
  print good nothing needs you right now
end

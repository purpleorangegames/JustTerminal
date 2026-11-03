# visitors: traders, guests and other people on the map, by faction

if /visitors/count = 0
  print dim no visitors
  stop
end

last = ""
for v in /visitors/list
  # the faction's name before its first person, the list comes sorted by faction
  if v/faction != last
    out bright {v/faction}
    if v/relation = "ally"
      out good   {v/relation}
    else if v/relation != none
      out warn   {v/relation}
    end
    print
    last = v/faction
  end
  out   {pad(v/name, 16)}
  out dim {pad(v/kind, 20)}
  if v/why != none
    out dim {v/why}
  end
  if v/downed = yes
    out warn   downed
  end
  print
end

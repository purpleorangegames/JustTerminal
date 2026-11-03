# critical: wounds no one has treated yet, worst bleeding first
# /health/list has everyone of ours, open has the wounds not treated

n = 0
for p in /health/list
  if count(p/open) > 0
    n = n + 1
    out bright {p/name}
    if p/bleeding > 0
      out bad   bleeding {round(p/bleeding)}%/day
      if p/dies != none
        out bad , dies in {p/dies}
      end
    end
    if p/downed = yes
      out warn   downed
    end
    print
    for w in p/open
      part = ""
      if w/part != none
        part = " · " + w/part
      end
      if w/bleeding = yes
        print bad   • {w/name}{part}  {round(w/bleed)}%/day
      else
        print   • {w/name}{part}
      end
    end
  end
end

if n = 0
  print good nobody has untreated wounds
end

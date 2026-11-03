# who is hungry, with a bar for each one

n = 0
for p in /pawns
  if p/food != none and p/food < 30
    out warn {pad(p/name, 14)}
    bar p/food 10
    print
    n = n + 1
  end
end
if n = 0
  print good nobody is hungry
end

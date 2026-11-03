# idle: colonists with nothing to do
# cp /bin/idle.sh ~/bin to make your own, it runs instead of this one

count = 0
for p in /pawns
  if p/idle = yes
    out bright {pad(p/name, 16)}
    print dim {cap(p/job)}
    count = count + 1
  end
end

if count = 0
  print good everyone is busy
else
  print warn {count} with nothing to do
end

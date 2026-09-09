#!/usr/bin/bash

./erm_converter.py \
  --output implementation/model.cs \
  --namespace TasModel \
  --entity_base TasEntity \
  --virtual-base nullable-fks \
  --virtual-base organization=base-and-kind \
  model/*.excalidraw
